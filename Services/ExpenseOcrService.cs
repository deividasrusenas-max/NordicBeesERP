using Azure;
using Azure.Core;
using Azure.AI.DocumentIntelligence;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services.Dtos;
using NordicBeesERP.Services.Validation;
using System.Globalization;
using NordicBeesERP.Helpers;

namespace NordicBeesERP.Services
{
    public class ExpenseOcrService : IExpenseOcrService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
        private readonly IViesService _viesService;
        private readonly ICompanySettingsService _companySettingsService;
        private readonly ILogger<ExpenseOcrService> _logger;

        private const string ModelId = "prebuilt-invoice";

        public ExpenseOcrService(IDbContextFactory<NordicBeesERPContext> dbFactory, IViesService viesService, ICompanySettingsService companySettingsService, ILogger<ExpenseOcrService> logger)
        {
            _dbFactory = dbFactory;
            _viesService = viesService;
            _companySettingsService = companySettingsService;
            _logger = logger;
        }

        private async Task<(string endpoint, string apiKey)> GetAzureCredentialsAsync()
        {
            await using var context = _dbFactory.CreateDbContext();
            var settings = await context.AppSettings
                .Where(s => s.SettingKey == "azure_di_endpoint" || s.SettingKey == "azure_di_key")
                .ToListAsync();
            var endpoint = settings.FirstOrDefault(s => s.SettingKey == "azure_di_endpoint")?.SettingValue ?? "";
            var apiKey = settings.FirstOrDefault(s => s.SettingKey == "azure_di_key")?.SettingValue ?? "";
            return (endpoint, apiKey);
        }

        public async Task<bool> IsAzureHealthyAsync()
        {
            var (endpoint, apiKey) = await GetAzureCredentialsAsync();
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey)) return false;

            try
            {
                var client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The Azure DI call: the raw analyze-response JSON, or null when Azure is not configured
        /// (<c>Diagnostics.AzureError</c> is set). Virtual only so tests can feed a recorded response
        /// through <see cref="ProcessAsync"/>; nothing else overrides it.
        /// </summary>
        protected virtual async Task<string?> AnalyzeInvoiceAsync(string base64, string fileName, OcrResultDto result)
        {
            var (endpoint, apiKey) = await GetAzureCredentialsAsync();
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
            {
                result.Diagnostics.AzureError = "Azure DI kredencialai nesukonfigūruoti";
                return null;
            }

            var client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

            result.OcrPipeline = ModelId;
            result.Diagnostics.AzureReachable = true;

            _logger.LogDebug("[AZURE DI] Analysing: {FileName}", fileName);

            using var requestContent = RequestContent.Create(
                new { base64Source = base64 }
            );

            var operation = await client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                ModelId,
                requestContent,
                locale: "lt-LT",
                pages: "1-2"
            );

            return operation.Value.ToString();
        }

        public async Task<OcrResultDto> ProcessAsync(string base64, string fileName)
        {
            var result = new OcrResultDto();
            
            try
            {
                var json = await AnalyzeInvoiceAsync(base64, fileName, result);
                if (json == null) return result;
                result.RawJson = json;
                var root = JsonDocument.Parse(json).RootElement;
                
                // JSON structure: { analyzeResult: { documents: [...] } }
                JsonElement documentsEl;
                if (root.TryGetProperty("analyzeResult", out var analyzeResult))
                {
                    if (!analyzeResult.TryGetProperty("documents", out documentsEl) &&
                        !analyzeResult.TryGetProperty("Documents", out documentsEl))
                    {
                        result.Diagnostics.AzureError = "Azure DI: nėra documents lauko";
                        return result;
                    }
                }
                else if (!root.TryGetProperty("Documents", out documentsEl) && 
                         !root.TryGetProperty("documents", out documentsEl))
                {
                    result.Diagnostics.AzureError = "Azure DI: nėra Documents lauko";
                    return result;
                }
                
                var documents = documentsEl;
                if (documents.GetArrayLength() == 0)
                {
                    result.Diagnostics.AzureError = "Azure DI negavo dokumentų";
                    return result;
                }

                var docRoot = documents[0];
                // Fields are under "fields" key
                JsonElement invoice;
                if (docRoot.TryGetProperty("fields", out var fieldsEl))
                    invoice = fieldsEl;
                else if (docRoot.TryGetProperty("Fields", out var fieldsEl2))
                    invoice = fieldsEl2;
                else
                    invoice = docRoot;

                // Helper method to get field value from JSON
                bool TryGetField(string fieldName, out JsonElement field)
                {
                    field = default;
                    if (!invoice.TryGetProperty(fieldName, out field))
                        return false;
                    return field.ValueKind != JsonValueKind.Null;
                }

                // Helper method to get nested field value
                bool TryGetFieldProperty(JsonElement field, string propertyName, out JsonElement property)
                {
                    property = default;
                    if (!field.TryGetProperty(propertyName, out property))
                        return false;
                    return property.ValueKind != JsonValueKind.Null;
                }

                // Get VendorName
                if (TryGetField("VendorName", out var vendorNameField))
                {
                    result.SupplierName = vendorNameField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" :
                                         vendorNameField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" :
                                         vendorNameField.TryGetProperty("Content", out var cp2) ? cp2.GetString() ?? "" : "";
                    if (vendorNameField.TryGetProperty("confidence", out var confidence))
                        result.Confidence.SupplierName = ToConfidencePercent((float)confidence.GetDouble());
                }

                // Get VendorTaxId
                if (TryGetField("VendorTaxId", out var vendorTaxIdField))
                {
                    result.SupplierVatCode = CleanVatCode(vendorTaxIdField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : vendorTaxIdField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "");
                }

                // Get VendorAddress
                if (TryGetField("VendorAddress", out var vendorAddressField))
                {
                    if (vendorAddressField.TryGetProperty("valueAddress", out var addrObj))
                    {
                        if (addrObj.TryGetProperty("streetAddress", out var street)) result.SupplierAddress = street.GetString() ?? "";
                        // Reorder address: "106L Marvelės g." → "Marvelės g. 106L"
                        if (!string.IsNullOrEmpty(result.SupplierAddress))
                        {
                            var addrParts = result.SupplierAddress.Split(' ');
                            if (addrParts.Length >= 2 && System.Text.RegularExpressions.Regex.IsMatch(addrParts[0], @"^\d+\w*$"))
                                result.SupplierAddress = string.Join(" ", addrParts.Skip(1)) + " " + addrParts[0];
                        }
                        if (addrObj.TryGetProperty("city", out var city)) result.SupplierCity = city.GetString() ?? "";
                        if (addrObj.TryGetProperty("postalCode", out var zip)) result.SupplierPostalCode = zip.GetString() ?? "";
                        if (addrObj.TryGetProperty("countryRegion", out var country)) { result.SupplierCountryCode = country.GetString() ?? ""; result.SupplierCountryCode = NormalizeCountryCode(result.SupplierCountryCode); }
                    }
                    else if (vendorAddressField.TryGetProperty("content", out var cp))
                        result.SupplierAddress = cp.GetString() ?? "";
                }

                // If VendorName looks like a logo/brand (no legal form), prefer VendorAddressRecipient
                if (!string.IsNullOrEmpty(result.SupplierName) && TryGetField("VendorAddressRecipient", out var nameRecipientField))
                {
                    var recipientName = nameRecipientField.TryGetProperty("valueString", out var rnvs) ? rnvs.GetString() ?? "" : "";
                    string[] legalForms = { "MB", "UAB", "AB", "VšĮ", "IĮ", "ŽŪB", "ŪB", "SIA", "OÜ", "AS", "GmbH", "Ltd", "SRL", "BV", "NV" };
                    bool recipientHasLegalForm = legalForms.Any(f => recipientName.Contains(f, StringComparison.OrdinalIgnoreCase));
                    bool vendorNameHasLegalForm = legalForms.Any(f => result.SupplierName.Contains(f, StringComparison.OrdinalIgnoreCase));
                    if (recipientHasLegalForm && !vendorNameHasLegalForm && !string.IsNullOrEmpty(recipientName))
                    {
                _logger.LogDebug("[VENDOR NAME] Preferring VendorAddressRecipient '{Recipient}' over VendorName '{Name}'", recipientName, result.SupplierName);
                        result.SupplierName = recipientName;
                    }
                }

                // Universal EU company code extraction
                // Priority: VendorTaxId → VendorBusinessNumber → VendorAddressRecipient → LT regex fallback
                if (string.IsNullOrEmpty(result.SupplierCompanyCode))
                {
                    // 1. Try VendorTaxId first (e.g., "LT123456789" for LT, DE123456789 for DE, etc.)
                    if (TryGetField("VendorTaxId", out var companyCodeTaxIdField))
                    {
                        var taxId = companyCodeTaxIdField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" :
                                    companyCodeTaxIdField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                        if (!string.IsNullOrWhiteSpace(taxId))
                        {
                            // Extract just the numeric part (remove country prefix like LT, DE, PL, etc.)
                            var cleanTaxId = CleanVatCode(taxId);
                            if (!string.IsNullOrWhiteSpace(cleanTaxId))
                            {
                                result.SupplierCompanyCode = cleanTaxId;
                                _logger.LogDebug("[COMPANY CODE] source=VendorTaxId value={Code}", result.SupplierCompanyCode);
                            }
                        }
                    }
                }

                // 2. Try VendorBusinessNumber if still empty
                if (string.IsNullOrEmpty(result.SupplierCompanyCode) && TryGetField("VendorBusinessNumber", out var businessNumberField))
                {
                    var businessNumber = businessNumberField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" :
                                         businessNumberField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(businessNumber))
                    {
                        result.SupplierCompanyCode = businessNumber.Trim();
                        _logger.LogDebug("[COMPANY CODE] source=VendorBusinessNumber value={Code}", result.SupplierCompanyCode);
                    }
                }

                // 3. Try VendorAddressRecipient full value if still empty
                if (string.IsNullOrEmpty(result.SupplierCompanyCode) && TryGetField("VendorAddressRecipient", out var recipientField))
                {
                    var recipient = recipientField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(recipient))
                    {
                        // Use the full value as company code (Azure may return it as a single identifier)
                        result.SupplierCompanyCode = recipient.Trim();
                        _logger.LogDebug("[COMPANY CODE] source=VendorAddressRecipient value={Code}", result.SupplierCompanyCode);
                    }
                }

                // 4. LT-only regex fallback: only if SupplierCountryCode == "LT" and still empty
                if (string.IsNullOrEmpty(result.SupplierCompanyCode) && result.SupplierCountryCode == "LT")
                {
                    if (TryGetField("VendorAddressRecipient", out var ltRecipientField))
                    {
                        var ltRecipient = ltRecipientField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : "";
                        var codeMatch = System.Text.RegularExpressions.Regex.Match(ltRecipient, @"\b\d{9}\b");
                        if (codeMatch.Success)
                        {
                            result.SupplierCompanyCode = codeMatch.Value;
                            _logger.LogDebug("[COMPANY CODE] source=LT_regex_fallback value={Code}", result.SupplierCompanyCode);
                        }
                    }
                }

                // Get PaymentDetails (bank account)
                if (TryGetField("PaymentDetails", out var paymentDetailsField))
                {
                    if (paymentDetailsField.TryGetProperty("valueArray", out var pdArray) && pdArray.GetArrayLength() > 0)
                    {
                        var firstPd = pdArray[0];
                        if (firstPd.TryGetProperty("valueObject", out var pdObj))
                        {
                            if (pdObj.TryGetProperty("IBAN", out var ibanField))
                                result.SupplierBankAccount = ibanField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : "";
                            if (string.IsNullOrEmpty(result.SupplierBankAccount) && pdObj.TryGetProperty("AccountNumber", out var accField))
                                result.SupplierBankAccount = accField.TryGetProperty("valueString", out var vs2) ? vs2.GetString() ?? "" : "";
                        }
                    }
                }

                // Get VendorPhone
                if (TryGetField("VendorPhone", out var vendorPhoneField))
                {
                    result.SupplierPhone = vendorPhoneField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : vendorPhoneField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                }

                // Get VendorEmail
                if (TryGetField("VendorEmail", out var vendorEmailField))
                {
                    result.SupplierEmail = vendorEmailField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : vendorEmailField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                }

                // Get InvoiceId
                if (TryGetField("InvoiceId", out var invoiceIdField))
                {
                    result.InvoiceNumber = invoiceIdField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : invoiceIdField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                    if (invoiceIdField.TryGetProperty("confidence", out var confidence))
                        result.Confidence.InvoiceNumber = ToConfidencePercent((float)confidence.GetDouble());
                }

                // Clean up InvoiceNumber — only remove specific known prefixes like "Serija DB Nr. 3022375"
                if (!string.IsNullOrEmpty(result.InvoiceNumber))
                {
                    var prefixMatch = System.Text.RegularExpressions.Regex.Match(
                        result.InvoiceNumber.Trim(),
                        @"^(?:Serija\s+\w+\s+)?Nr\.\s*(.+)$",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (prefixMatch.Success)
                    {
                        var cleaned = prefixMatch.Groups[1].Value.Trim();
                        _logger.LogDebug("[INV NUMBER] Cleaned prefix: '{Old}' → '{New}'", result.InvoiceNumber, cleaned);
                        result.InvoiceNumber = cleaned;
                    }
                }

                // Get InvoiceDate
                if (TryGetField("InvoiceDate", out var invoiceDateField))
                {
                    if (invoiceDateField.TryGetProperty("valueDate", out var valueDate) || invoiceDateField.TryGetProperty("ValueDate", out valueDate))
                    {
                        result.InvoiceDate = valueDate.GetString();
                        if (invoiceDateField.TryGetProperty("confidence", out var confidence))
                            result.Confidence.InvoiceDate = ToConfidencePercent((float)confidence.GetDouble());
                    }
                }

                // Get DueDate
                if (TryGetField("DueDate", out var dueDateField))
                {
                    if (dueDateField.TryGetProperty("valueDate", out var valueDate) || dueDateField.TryGetProperty("ValueDate", out valueDate))
                    {
                        result.DueDate = valueDate.GetString();
                        if (dueDateField.TryGetProperty("confidence", out var confidence))
                            result.Confidence.DueDate = ToConfidencePercent((float)confidence.GetDouble());
                    }
                }

                _logger.LogDebug("[DATE] InvoiceDate={InvoiceDate} DueDate={DueDate}", result.InvoiceDate, result.DueDate);

                // Fallback: if DueDate not found, try PaymentTerm and calculate from InvoiceDate
                if (string.IsNullOrEmpty(result.DueDate) && !string.IsNullOrEmpty(result.InvoiceDate))
                {
                    if (TryGetField("PaymentTerm", out var paymentTermField))
                    {
                        var termStr = paymentTermField.TryGetProperty("valueString", out var pts) ? pts.GetString() ?? "" :
                                      paymentTermField.TryGetProperty("content", out var ptcp) ? ptcp.GetString() ?? "" : "";
                        var termMatch = System.Text.RegularExpressions.Regex.Match(termStr, @"\d+");
                        if (termMatch.Success && int.TryParse(termMatch.Value, out var days) && days > 0 && days <= 365)
                        {
                            if (DateTime.TryParse(result.InvoiceDate, out var invDate))
                            {
                                result.DueDate = invDate.AddDays(days).ToString("yyyy-MM-dd");
                                _logger.LogDebug("[DUE DATE] Calculated from PaymentTerm={Term}: {DueDate}", termStr, result.DueDate);
                            }
                        }
                    }
                }

                // Get CustomerName (BilledTo / CustomerName)
                if (TryGetField("CustomerName", out var customerNameField))
                {
                    result.CustomerName = customerNameField.TryGetProperty("valueString", out var cvs) ? cvs.GetString() ?? "" :
                                          customerNameField.TryGetProperty("content", out var ccp) ? ccp.GetString() ?? "" : "";
                }
                if (string.IsNullOrEmpty(result.CustomerName) && TryGetField("BilledTo", out var billedToField))
                {
                    result.CustomerName = billedToField.TryGetProperty("valueString", out var bts) ? bts.GetString() ?? "" :
                                          billedToField.TryGetProperty("content", out var btc) ? btc.GetString() ?? "" : "";
                }

                // Get CustomerTaxId (buyer VAT code for WRONG_RECIPIENT check)
                if (TryGetField("CustomerTaxId", out var customerTaxIdField))
                {
                    result.CustomerVatCode = CleanVatCode(customerTaxIdField.TryGetProperty("valueString", out var cts) ? cts.GetString() ?? "" :
                                              customerTaxIdField.TryGetProperty("content", out var ctc) ? ctc.GetString() ?? "" : "");
                }

                // Header totals (SubTotal, TotalTax, InvoiceTotal) with the printed text next to each (D-041)
                OcrNumberReads.ReadHeaderTotals(invoice, result);

                // Get TaxDetails from Items to find first non-zero VAT rate
                bool hasItems = invoice.TryGetProperty("Items", out var itemsField) || 
                                invoice.TryGetProperty("items", out itemsField);
                JsonElement actualItems = itemsField;
                if (hasItems)
                {
                    if (itemsField.ValueKind == JsonValueKind.Object)
                    {
                        if (itemsField.TryGetProperty("valueArray", out var va)) actualItems = va;
                        else if (itemsField.TryGetProperty("values", out var vv)) actualItems = vv;
                    }
                }
                
                var hasInvalidVatRate = false;

                // Extract VAT rate from items (first non-zero rate)
                if (hasItems && actualItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in actualItems.EnumerateArray())
                        {
                            if (!item.TryGetProperty("valueObject", out var valueObject) && !item.TryGetProperty("ValueObject", out valueObject) || valueObject.ValueKind == JsonValueKind.Null)
                                continue;

                            var f = valueObject;
                            
                            // Get TaxRate from item
                            if (f.TryGetProperty("TaxRate", out var taxRateField))
                            {
                                var rateStr = taxRateField.TryGetProperty("valueString", out var vs) ? vs.GetString()?.TrimEnd('%').Trim() ?? "" : taxRateField.TryGetProperty("content", out var cp) ? cp.GetString()?.TrimEnd('%').Trim() ?? "" : "";
                                var parsedRate = ParseVatRate(rateStr);
                                if (parsedRate.HasValue)
                                {
                                    _logger.LogInformation("[VAT RATE] raw={Raw} parsed={Parsed}", rateStr, parsedRate.Value);

                                    if (parsedRate.Value > 0 && result.VatRate == 0)
                                    {
                                        result.VatRate = parsedRate.Value;
                                        break;
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(rateStr))
                                {
                                    _logger.LogWarning("[VAT RATE] raw={Raw} is not a valid 0..100 rate", rateStr);
                                    hasInvalidVatRate = true;
                                }
                            }
                        }
                    }

                    // Fallback: derive VAT rate from totals if not found in items
                if (result.VatRate == 0 && result.AmountExclVat > 0 && result.VatAmount > 0)
                    result.VatRate = Math.Round(result.VatAmount / result.AmountExclVat * 100, 0);

                // Extract line items
                if (hasItems && actualItems.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in actualItems.EnumerateArray())
                    {
                        if (!item.TryGetProperty("valueObject", out var valueObject) && !item.TryGetProperty("ValueObject", out valueObject) || valueObject.ValueKind == JsonValueKind.Null)
                            continue;

                        var lineDto = new OcrLineDto();
                        var f = valueObject;

                        // Get Description
                        if (f.TryGetProperty("Description", out var descField))
                            lineDto.Description = descField.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" : descField.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";

                        // Fallback: try ProductCode as description
                        if (string.IsNullOrEmpty(lineDto.Description) && f.TryGetProperty("ProductCode", out var productCodeField))
                        {
                            lineDto.Description = productCodeField.TryGetProperty("valueString", out var vs2) ? vs2.GetString() ?? "" :
                                                  productCodeField.TryGetProperty("content", out var cp2) ? cp2.GetString() ?? "" : "";
                            _logger.LogDebug("[LINE DESC] Using ProductCode as description: {Desc}", lineDto.Description);
                        }

                        // Try ProductDescription if still empty
                        if (string.IsNullOrEmpty(lineDto.Description) && f.TryGetProperty("ProductDescription", out var prodDescField))
                        {
                            lineDto.Description = prodDescField.TryGetProperty("valueString", out var vs3d) ? vs3d.GetString() ?? "" :
                                                  prodDescField.TryGetProperty("content", out var cp3d) ? cp3d.GetString() ?? "" : "";
                        }

                        // Quantity, UnitPrice and Amount (Net as its fallback) with the printed text next to each (D-041)
                        OcrNumberReads.ReadLineNumbers(f, lineDto);

                        // Get UnitOfMeasure
                        if (f.TryGetProperty("Unit", out var unitField))
                            lineDto.UnitOfMeasure = unitField.TryGetProperty("valueString", out var vs3) ? vs3.GetString() ?? "" :
                                                    unitField.TryGetProperty("content", out var cp3) ? cp3.GetString() ?? "" : "";

                        // Get line confidence from Azure DI
                        if (item.TryGetProperty("confidence", out var lineConfEl))
                            lineDto.Confidence = (decimal)lineConfEl.GetDouble();

                        // Get TaxRate for line
                        if (f.TryGetProperty("TaxRate", out var taxRateField))
                        {
                            var rateStr = taxRateField.TryGetProperty("valueString", out var vs) ? vs.GetString()?.TrimEnd('%').Trim() ?? "" : taxRateField.TryGetProperty("content", out var cp) ? cp.GetString()?.TrimEnd('%').Trim() ?? "" : "";
                            var parsedRate = ParseVatRate(rateStr);
                            if (parsedRate.HasValue)
                            {
                                _logger.LogInformation("[VAT RATE] line={Desc} raw={Raw} parsed={Parsed}", lineDto.Description, rateStr, parsedRate.Value);

                                lineDto.VatRate = parsedRate.Value;
                            }
                            else if (!string.IsNullOrWhiteSpace(rateStr))
                            {
                                _logger.LogWarning("[VAT RATE] line={Desc} raw={Raw} is not a valid 0..100 rate", lineDto.Description, rateStr);
                                hasInvalidVatRate = true;
                            }
                        }

                        // Get TaxAmount for VAT rate calculation if not set
                        if (lineDto.VatRate == 0 && f.TryGetProperty("TaxAmount", out var taxAmountField) &&
                            ( taxAmountField.TryGetProperty("valueCurrency", out var valueCurrency) || taxAmountField.TryGetProperty("ValueCurrency", out valueCurrency)) && lineDto.AmountExclVat > 0)
                        {
                            if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
                            {
                                var ta = (decimal)amount.GetDouble();
                                if (ta > 0)
                                    lineDto.VatRate = Math.Round(ta / lineDto.AmountExclVat * 100, 0);
                            }
                        }

                        // Fallback: copy VAT rate from header
                        if (lineDto.VatRate == 0 && result.VatRate > 0)
                            lineDto.VatRate = result.VatRate;

                        // Calculate incl VAT
                        if (lineDto.AmountExclVat > 0)
                            lineDto.AmountInclVat = lineDto.VatRate > 0
                                ? Math.Round(lineDto.AmountExclVat * (1 + lineDto.VatRate / 100), 2)
                                : lineDto.AmountExclVat;
                        else if (lineDto.UnitPrice.HasValue && lineDto.Quantity.HasValue)
                        {
                            lineDto.AmountExclVat = Math.Round(lineDto.UnitPrice.Value * lineDto.Quantity.Value, 2);
                            lineDto.NetDerived = true;
                            lineDto.AmountInclVat = lineDto.VatRate > 0
                                ? Math.Round(lineDto.AmountExclVat * (1 + lineDto.VatRate / 100), 2)
                                : lineDto.AmountExclVat;
                        }

                        _logger.LogDebug("[AZURE LINE] desc={Desc} qty={Qty} excl={Excl} vat={VatRate}% incl={Incl}", 
                            lineDto.Description, lineDto.Quantity, lineDto.AmountExclVat, lineDto.VatRate, lineDto.AmountInclVat);

                        // Skip lines that are clearly metadata (Svoris/Weight with 0 amount)
                        bool isMetadataLine = lineDto.AmountExclVat == 0 && lineDto.AmountInclVat == 0 &&
                                              !lineDto.UnitPrice.HasValue &&
                                              (lineDto.Description.Contains("voris", StringComparison.OrdinalIgnoreCase) ||
                                               lineDto.Description.Contains("weight", StringComparison.OrdinalIgnoreCase) ||
                                               lineDto.Description.Contains("Svoris", StringComparison.OrdinalIgnoreCase));
                        if (isMetadataLine)
                        {
                            _logger.LogDebug("[LINE SKIP] Skipping metadata line: {Desc}", lineDto.Description);
                            continue;
                        }

                        // Include line if it has Description OR has a meaningful amount
                        bool hasDescription = !string.IsNullOrEmpty(lineDto.Description);
                        bool hasAmount = lineDto.AmountExclVat > 0 || lineDto.AmountInclVat > 0 ||
                                         (lineDto.UnitPrice.HasValue && lineDto.UnitPrice.Value > 0 && lineDto.Quantity.HasValue);
                        if (hasDescription || hasAmount)
                        {
                            if (string.IsNullOrEmpty(lineDto.Description))
                                lineDto.Description = $"Eilutė {result.Lines.Count + 1}";
                            result.Lines.Add(lineDto);
                        }
                    }
                }

                // Locale-number detection (D-041) reads the lines as Azure returned them, so it runs BEFORE the
                // reconcile step below can drop anything (PLAN-ETAPAS1 §3.1). Detection only: no value is replaced.
                ExpenseService.RecomputeNumberReadFlags(result.Flags, result);

                // =====================================================
                // POST-PROCESSING: Reconcile lines against header totals
                // =====================================================
                ReconcileLines(result, _logger);

                _logger.LogDebug("[AZURE DI] supplier={Supplier} vat={Vat} inv={Invoice} total={Total}",
                    result.SupplierName, result.SupplierVatCode, result.InvoiceNumber, result.AmountInclVat);

                // Load company settings early for VIES own-company check
                var settings = await _companySettingsService.GetSettingsAsync();

                var defaultCategoryId = await ResolveSupplierAsync(result, settings);
                var supplierId = result.SupplierId;

                // Auto-assign category from supplier default if result.CategoryId is null
                if (supplierId != null && defaultCategoryId != null && result.CategoryId == null)
                {
                    result.CategoryId = defaultCategoryId;
                    _logger.LogDebug("[CATEGORY] Auto-assigned from supplier: CategoryId={CategoryId}", result.CategoryId);
                }

                // Fallback: load DefaultExpenseCategoryId directly from BusinessPartners if still null
                if (result.CategoryId == null && supplierId.HasValue)
                {
                    await using var ctx2 = _dbFactory.CreateDbContext();
                    var fallbackCategory = await ctx2.BusinessPartners
                        .Where(b => b.Id == supplierId)
                        .Select(b => b.DefaultExpenseCategoryId)
                        .FirstOrDefaultAsync();
                    if (fallbackCategory.HasValue)
                    {
                        result.CategoryId = fallbackCategory;
                        _logger.LogDebug("[CATEGORY] Loaded fallback from BusinessPartners: CategoryId={CategoryId}", result.CategoryId);
                    }
                }

                _logger.LogDebug("[CATEGORY] supplier={SupplierId} defaultCategory={CategoryId}", result.SupplierId, result.CategoryId);

                // Build flags
                // VENDOR_NOT_FOUND: result.SupplierId == null
                if (result.SupplierId == null)
                    result.Flags.Add(OcrFlag.VendorNotFound);

                // WRONG_RECIPIENT: !string.IsNullOrEmpty(result.CustomerVatCode) && result.CustomerVatCode != settings.VatCode && !result.CustomerName.Contains(settings.CompanyName, StringComparison.OrdinalIgnoreCase)
                if (!string.IsNullOrEmpty(result.CustomerVatCode) && 
                    result.CustomerVatCode != settings.VatCode && 
                    !result.CustomerName.Contains(settings.CompanyName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Flags.Add(OcrFlag.WrongRecipient);
                }

                // MISSING_AMOUNT: result.AmountInclVat == 0
                if (result.AmountInclVat == 0)
                    result.Flags.Add(OcrFlag.MissingAmount);

                // MISSING_INV_NUMBER: string.IsNullOrEmpty(result.InvoiceNumber)
                if (string.IsNullOrEmpty(result.InvoiceNumber))
                    result.Flags.Add(OcrFlag.MissingInvNumber);

                // MISSING_DUE_DATE: string.IsNullOrEmpty(result.DueDate)
                if (string.IsNullOrEmpty(result.DueDate))
                    result.Flags.Add(OcrFlag.MissingDueDate);

                // ZERO_VAT: result.VatRate == 0 && result.AmountInclVat > 0
                if (result.VatRate == 0 && result.AmountInclVat > 0)
                    result.Flags.Add(OcrFlag.ZeroVat);

                // INVALID_VAT_RATE: raw OCR VAT-rate text was present but could not be parsed as 0..100
                if (hasInvalidVatRate)
                    result.Flags.Add(OcrFlag.InvalidVatRate);

                // LINES_NOT_FOUND: result.Lines.Count == 0
                if (result.Lines.Count == 0)
                    result.Flags.Add(OcrFlag.LinesNotFound);

                // EN 16931 gates (AMOUNT_ARITHMETIC_MISMATCH / MISSING_MONEY_FIELD / TOTALS_OUT_OF_RANGE,
                // AMOUNT_MISMATCH / LINE_SUM_ROUNDING by BR-CO-10, D-040): the same helper the save paths
                // use, so the preview shows what will be stored
                // (the VAT-rate whitelist too: the partner's country when a supplier matched, the document's otherwise)
                string? partnerCountry = null;
                if (supplierId.HasValue)
                {
                    await using var countryContext = _dbFactory.CreateDbContext();
                    partnerCountry = await countryContext.BusinessPartners.Where(b => b.Id == supplierId.Value)
                        .Select(b => b.CountryCode).FirstOrDefaultAsync();
                }
                DateTime? invoiceDate = DateTime.TryParse(result.InvoiceDate, out var parsedDate) && parsedDate != default ? parsedDate : null;
                ExpenseService.RecomputeValidationFlags(result.Flags, result.AmountExclVat, result.VatAmount,
                    result.AmountInclVat, ExpenseService.ToValidationLines(result.Lines), ExpenseService.ToDocumentInput(result),
                    ExpenseService.ToRateInput(result, partnerCountry, invoiceDate, "STANDARD"));

                // LOW_CONFIDENCE: result.Confidence.Overall > 0 && result.Confidence.Overall < 50
                if (result.Confidence.Overall > 0 && result.Confidence.Overall < 50)
                    result.Flags.Add(OcrFlag.LowConfidence);

                result.LinesMatchHeader = !result.Flags.Contains(OcrFlag.AmountMismatch);
                result.Success = true;
            }
            catch (RequestFailedException ex) when (ex.ErrorCode == "429")
            {
                _logger.LogWarning("[AZURE DI] Rate limit exceeded: {Message}", ex.Message);
                result.Flags.Add(OcrFlag.AzureLimit);
                result.Success = false;
                result.Diagnostics.AzureError = "Azure DI rate limit exceeded (429)";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AZURE DI ERROR] {Type}: {Message}", ex.GetType().Name, ex.Message);
                result.Diagnostics.AzureReachable = false;
                result.Diagnostics.AzureError = ex.Message;
                result.Success = false;
            }

            return result;
        }

        /// <summary>
        /// Post-processing: reconcile the lines against the header total. Lines that exceed the header by more than
        /// 0.05 € lose their zero-amount lines (nothing to lose there). D-038 Q7 / D-041: the two steps that used to
        /// delete real lines — quantity &gt; 1000 („likely weight") and repeated descriptions — no longer delete
        /// anything; where they would have acted the lines are kept and the invoice is marked with the information
        /// flags LINE_LARGE_QUANTITY / LINE_DUPLICATE_DESCRIPTION (BR-CO-10 already holds it in review). Lines exceeding
        /// the header is never "fixed" by dropping data. Lines below the header: nothing (the user adds lines by hand).
        /// </summary>
        public static void ReconcileLines(OcrResultDto result, ILogger logger)
        {
            if (!result.Lines.Any() || result.AmountExclVat <= 0) return;

            var linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
            var diff = linesSumExcl - result.AmountExclVat;
            if (diff <= 0.05m) return;

            logger.LogDebug("[RECONCILE] Lines exceed header by {Diff}. Removing zero-amount lines only.", diff);

            // Step 1: remove zero-amount lines only
            foreach (var candidate in result.Lines.Where(l => l.AmountExclVat == 0).ToList())
            {
                result.Lines.Remove(candidate);
                logger.LogDebug("[RECONCILE] Removed zero-amount line (conf={Conf}): {Desc}", candidate.Confidence, candidate.Description);
            }

            diff = result.Lines.Sum(l => l.AmountExclVat) - result.AmountExclVat;
            if (diff > 0.05m)
            {
                if (result.Lines.Any(l => l.Quantity.HasValue && l.Quantity.Value > 1000)
                    && !result.Flags.Contains(OcrFlag.LineLargeQuantity))
                    result.Flags.Add(OcrFlag.LineLargeQuantity);

                if (result.Lines.GroupBy(l => l.Description).Any(g => g.Count() > 1)
                    && !result.Flags.Contains(OcrFlag.LineDuplicateDescription))
                    result.Flags.Add(OcrFlag.LineDuplicateDescription);
            }

            logger.LogDebug("[RECONCILE] After cleanup: LinesSumExcl={Sum} HeaderExcl={Header}",
                result.Lines.Sum(l => l.AmountExclVat), result.AmountExclVat);
        }

        /// <summary>
        /// VIES lookup, own-company check, country/name normalisation and supplier match for an extracted
        /// invoice — the part of <see cref="ProcessAsync"/> that runs after the amounts are read. A
        /// malformed VAT code (<see cref="VatCodeFormatValidator"/> WrongFormat) skips VIES and the VAT
        /// match; the name match may still run. Returns the matched supplier's default expense category.
        /// </summary>
        public async Task<int?> ResolveSupplierAsync(OcrResultDto result, CompanySettings settings)
        {
            // VAT-code format gate (PLAN-ETAPAS1 §1.2, S5a): a malformed code is neither sent to VIES nor
            // used to match a supplier. The hint is the address country only — the prefix fallback below
            // runs later, so it cannot make a code agree with itself. INVALID_VAT_FORMAT is set here so the
            // stopped lookup is visible; ExpenseService.RecomputeValidationFlags re-derives it from the final values.
            var vatFormatWrong = VatCodeFormatValidator.Validate(result.SupplierVatCode, result.SupplierCountryCode).Reason
                == VatCodeValidationReason.WrongFormat;
            if (vatFormatWrong && !result.Flags.Contains(OcrFlag.InvalidVatFormat))
                result.Flags.Add(OcrFlag.InvalidVatFormat);

            // VIES lookup
            if (!string.IsNullOrEmpty(result.SupplierVatCode) && !vatFormatWrong)
            {
                _logger.LogDebug("[VIES] Looking up: {VatCode}", result.SupplierVatCode);
                var viesResult = await _viesService.LookupAsync(result.SupplierVatCode);
                result.ViesServiceAvailable = viesResult.ServiceAvailable;

                if (viesResult.ServiceAvailable)
                {
                    if (viesResult.IsValid)
                    {
                        result.ViesVerified = true;
                        result.ViesName = viesResult.Name;
                        if (!string.IsNullOrEmpty(viesResult.Address) && viesResult.Address != "---")
                            result.ViesAddress = viesResult.Address;

                        // Check if this is our own company VAT code before overriding name
                        var isOwnCompany = !string.IsNullOrEmpty(result.SupplierVatCode) &&
                            string.Equals(result.SupplierVatCode, settings.VatCode, StringComparison.OrdinalIgnoreCase);

                        if (isOwnCompany)
                        {
                            // Keep original Azure DI vendor name, just add flag
                            result.Flags.Add(OcrFlag.OwnCompany);
                            result.PendingSupplierName = result.SupplierName;
                            _logger.LogDebug("[VIES] Own company detected, keeping vendor name: {Name} pendingSupplier={Pending}", result.SupplierName, result.PendingSupplierName);
                        }
                        else if (!string.IsNullOrEmpty(viesResult.Name) && viesResult.Name != "---" &&
                            !result.SupplierName.Equals(viesResult.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogDebug("[VIES] Overriding supplier name: '{Old}' -> '{New}'", result.SupplierName, viesResult.Name);
                            result.SupplierName = viesResult.Name;
                        }
                    }
                }
                else
                {
                    result.Flags.Add(OcrFlag.ViesUnavailable);
                }
            }

            // Own company check is now handled inside VIES section above

            // Normalize country code if still empty - take first 2 chars of VAT code
            if (string.IsNullOrEmpty(result.SupplierCountryCode) && !string.IsNullOrEmpty(result.SupplierVatCode) && result.SupplierVatCode.Length >= 2)
                result.SupplierCountryCode = NormalizeCountryCode(result.SupplierVatCode[..2]);

            // Normalize company name
            var normalized = CompanyNameHelper.Normalize(result.SupplierName);
            if (!normalized.Equals(result.SupplierName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[NAME NORMALIZE] '{Old}' -> '{New}'", result.SupplierName, normalized);
                result.SupplierName = normalized;
            }

            // Find supplier ID and DefaultExpenseCategoryId; a malformed VAT code never matches by VAT
            // (the name match still runs, as before)
            var (supplierId, defaultCategoryId) = await FindSupplierIdAsync(result.SupplierName, vatFormatWrong ? "" : result.SupplierVatCode);
            result.SupplierId = supplierId;
            return defaultCategoryId;
        }

        public async Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName)
            => await ProcessAsync(base64, fileName);

        public static decimal? ParseVatRate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            var normalized = raw
                .Trim()
                .TrimEnd('%')
                .Replace(",", ".")
                .Replace(" ", "");

            if (string.IsNullOrEmpty(normalized)) return null;
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate)) return null;
            if (rate < 0m || rate > 100m) return null;

            return rate;
        }

        public async Task<(int? supplierId, int? defaultCategoryId)> FindSupplierIdAsync(string supplierName, string vatCode)
        {
            // Normalise: trim, drop inner spaces, upper-case. An empty code must never reach the
            // VAT predicate — `VatCode == ""` would match any partner without a VAT code (D-017).
            var normalizedVat = (vatCode ?? "").Replace(" ", "").Trim().ToUpperInvariant();
            var trimmedName = (supplierName ?? "").Trim();

            if (normalizedVat.Length == 0 && trimmedName.Length == 0) return (null, null);

            await using var context = _dbFactory.CreateDbContext();

            _logger.LogDebug("[FIND SUPPLIER] name='{Name}' vat='{Vat}'", trimmedName, normalizedVat);

            // Try by VAT first, then fallback to name — return both Id and DefaultExpenseCategoryId.
            // Ambiguous matches (more than one distinct partner) return (null, null): a loud
            // VENDOR_NOT_FOUND is preferred over a silently wrong supplier (D-017).
            if (normalizedVat.Length > 0)
            {
                var withPrefix = "LT" + normalizedVat;
                var withoutPrefix = normalizedVat.StartsWith("LT") ? normalizedVat.Substring(2) : normalizedVat;

                var byVat = await context.BusinessPartners
                    .Where(bp => bp.VatCode != null && bp.VatCode != ""
                              && (bp.VatCode == normalizedVat
                                  || bp.VatCode == withPrefix
                                  || bp.VatCode == withoutPrefix))
                    .Select(bp => new { bp.Id, bp.DefaultExpenseCategoryId })
                    .Take(2)
                    .ToListAsync();

                if (byVat.Count > 1)
                {
                    _logger.LogDebug("[FIND SUPPLIER] ambiguous VAT match '{Vat}' — not assigning", normalizedVat);
                    return (null, null);
                }
                if (byVat.Count == 1)
                    return (byVat[0].Id, byVat[0].DefaultExpenseCategoryId);
            }

            if (trimmedName.Length > 0)
            {
                // Exact name match only (DB collation is case-insensitive); partial matches are not accepted.
                var byName = await context.BusinessPartners
                    .Where(bp => bp.Name == trimmedName)
                    .Select(bp => new { bp.Id, bp.DefaultExpenseCategoryId })
                    .Take(2)
                    .ToListAsync();

                if (byName.Count > 1)
                {
                    _logger.LogDebug("[FIND SUPPLIER] ambiguous name match '{Name}' — not assigning", trimmedName);
                    return (null, null);
                }
                if (byName.Count == 1)
                    return (byName[0].Id, byName[0].DefaultExpenseCategoryId);
            }

            return (null, null);
        }

        internal static int ToConfidencePercent(float? confidence) =>
            confidence.HasValue ? (int)Math.Round(confidence.Value * 100) : 0;

        private static string CleanVatCode(string raw)
        {
            // Remove spaces, dashes, dots that sometimes appear in extracted VAT codes
            return raw.Replace(" ", "").Replace("-", "").Replace(".", "").Trim();
        }

        private static string NormalizeCountryCode(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length == 2) return value.ToUpper();
            try { return new System.Globalization.RegionInfo(value).TwoLetterISORegionName; }
            catch { return value.Length >= 2 ? value[..2].ToUpper() : value.ToUpper(); }
        }
    }
}