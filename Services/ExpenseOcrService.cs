using Azure;
using Azure.Core;
using Azure.AI.DocumentIntelligence;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services.Dtos;
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

        public async Task<OcrResultDto> ProcessAsync(string base64, string fileName)
        {
            var result = new OcrResultDto();
            
            try
            {
                var (endpoint, apiKey) = await GetAzureCredentialsAsync();
                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                {
                    result.Diagnostics.AzureError = "Azure DI kredencialai nesukonfigūruoti";
                    return result;
                }

                var client = new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

                result.OcrPipeline = "AZURE_DI";
                result.Diagnostics.AzureReachable = true;

                _logger.LogDebug("[AZURE DI] Analysing: {FileName}", fileName);

                using var requestContent = RequestContent.Create(
                    new { base64Source = base64 }
                );

                var operation = await client.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    "prebuilt-invoice",
                    requestContent,
                    locale: "lt-LT",
                    pages: "1-2"
                );

                var json = operation.Value.ToString();
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

                // Get SubTotal
                if (TryGetField("SubTotal", out var subTotalField))
                {
                    if (subTotalField.TryGetProperty("valueCurrency", out var valueCurrency) || subTotalField.TryGetProperty("ValueCurrency", out valueCurrency))
                    {
                        if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
                            result.AmountExclVat = Math.Round((decimal)amount.GetDouble(), 2);
                    }
                }

                // Get TotalTax
                if (TryGetField("TotalTax", out var totalTaxField))
                {
                    if (totalTaxField.TryGetProperty("valueCurrency", out var valueCurrency) || totalTaxField.TryGetProperty("ValueCurrency", out valueCurrency))
                    {
                        if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
                            result.VatAmount = Math.Round((decimal)amount.GetDouble(), 2);
                    }
                }

                // Get InvoiceTotal
                if (TryGetField("InvoiceTotal", out var invoiceTotalField))
                {
                    if (invoiceTotalField.TryGetProperty("valueCurrency", out var valueCurrency) || invoiceTotalField.TryGetProperty("ValueCurrency", out valueCurrency))
                    {
                            if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
                            {
                                result.AmountInclVat = Math.Round((decimal)amount.GetDouble(), 2);
                            if (invoiceTotalField.TryGetProperty("confidence", out var confidence))
                                result.Confidence.Amounts = ToConfidencePercent((float)confidence.GetDouble());
                        }
                    }
                }

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
                                if (decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rateVal))
                                {
                                    // Guard: if rate > 100, likely encoded as basis points (e.g. 2300 = 23%)
                                    var parsedRate = rateVal;
                                    if (parsedRate > 100)
                                    {
                                        parsedRate = parsedRate / 100m;
                                    }

                                    _logger.LogInformation("[VAT RATE] raw={Raw} parsed={Parsed}", rateStr, parsedRate);

                                    if (parsedRate > 0 && result.VatRate == 0)
                                    {
                                        result.VatRate = parsedRate;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Fallback: derive VAT rate from totals if not found in items
                if (result.VatRate == 0 && result.AmountExclVat > 0 && result.VatAmount > 0)
                    result.VatRate = Math.Round(result.VatAmount / result.AmountExclVat * 100, 0);

                // Fallback: incl = excl + vat
                if (result.AmountInclVat == 0 && result.AmountExclVat > 0)
                    result.AmountInclVat = result.AmountExclVat + result.VatAmount;

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

                        // Get Quantity
                        if (f.TryGetProperty("Quantity", out var qtyField) && (qtyField.TryGetProperty("valueNumber", out var valueNumber) || qtyField.TryGetProperty("ValueNumber", out valueNumber)))
                            lineDto.Quantity = (decimal)valueNumber.GetDouble();

                        // Get UnitOfMeasure
                        if (f.TryGetProperty("Unit", out var unitField))
                            lineDto.UnitOfMeasure = unitField.TryGetProperty("valueString", out var vs3) ? vs3.GetString() ?? "" :
                                                    unitField.TryGetProperty("content", out var cp3) ? cp3.GetString() ?? "" : "";

                        // Get UnitPrice
                        if (f.TryGetProperty("UnitPrice", out var unitPriceField) && (unitPriceField.TryGetProperty("valueCurrency", out var valueCurrency) || unitPriceField.TryGetProperty("ValueCurrency", out valueCurrency)))
                        {
                            if (valueCurrency.TryGetProperty("amount", out var amount) || valueCurrency.TryGetProperty("Amount", out amount))
                                lineDto.UnitPrice = (decimal)amount.GetDouble();
                        }

                        // Get Amount (excl VAT line total)
                        if (f.TryGetProperty("Amount", out var amountField) && (amountField.TryGetProperty("valueCurrency", out var amountCurrency) || amountField.TryGetProperty("ValueCurrency", out amountCurrency)))
                        {
                            if (amountCurrency.TryGetProperty("amount", out var amountProp) || amountCurrency.TryGetProperty("Amount", out amountProp))
                                lineDto.AmountExclVat = (decimal)amountProp.GetDouble();
                        }

                        // Try Net field as fallback for Amount
                        if (lineDto.AmountExclVat == 0 && f.TryGetProperty("Net", out var netField) &&
                            (netField.TryGetProperty("valueCurrency", out var netCurrency) || netField.TryGetProperty("ValueCurrency", out netCurrency)))
                        {
                            if (netCurrency.TryGetProperty("amount", out var netAmt) || netCurrency.TryGetProperty("Amount", out netAmt))
                                lineDto.AmountExclVat = (decimal)netAmt.GetDouble();
                        }

                        // Get line confidence from Azure DI
                        if (item.TryGetProperty("confidence", out var lineConfEl))
                            lineDto.Confidence = (decimal)lineConfEl.GetDouble();

                        // Get TaxRate for line
                        if (f.TryGetProperty("TaxRate", out var taxRateField))
                        {
                            var rateStr = taxRateField.TryGetProperty("valueString", out var vs) ? vs.GetString()?.TrimEnd('%').Trim() ?? "" : taxRateField.TryGetProperty("content", out var cp) ? cp.GetString()?.TrimEnd('%').Trim() ?? "" : "";
                            if (decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rateVal))
                            {
                                // Guard: if rate > 100, likely encoded as basis points (e.g. 2300 = 23%)
                                var parsedRate = rateVal;
                                if (parsedRate > 100)
                                {
                                    parsedRate = parsedRate / 100m;
                                }

                                _logger.LogInformation("[VAT RATE] line={Desc} raw={Raw} parsed={Parsed}", lineDto.Description, rateStr, parsedRate);

                                lineDto.VatRate = parsedRate;
                            }
                        }

                        // Get TaxAmount for VAT rate calculation if not set
                        if (lineDto.VatRate == 0 && f.TryGetProperty("TaxAmount", out var taxAmountField) &&
                            ( taxAmountField.TryGetProperty("valueCurrency", out valueCurrency) || taxAmountField.TryGetProperty("ValueCurrency", out valueCurrency)) && lineDto.AmountExclVat > 0)
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

                // =====================================================
                // POST-PROCESSING: Reconcile lines against header totals
                // =====================================================
                if (result.Lines.Any() && result.AmountExclVat > 0)
                {
                    var linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
                    var diff = linesSumExcl - result.AmountExclVat;

                    // CASE 1: Lines sum EXCEEDS header (phantom lines from spec pages)
                    // Strategy: remove lines that cause the excess, starting from zero-amount or duplicate-description lines
                    if (diff > 0.05m)
                    {
                        _logger.LogDebug("[RECONCILE] Lines exceed header by {Diff}. Attempting to remove phantom lines.", diff);

                        // Step 1: Remove zero-amount lines only
                        var zeroAmountLines = result.Lines
                            .Where(l => l.AmountExclVat == 0)
                            .ToList();
                        foreach (var candidate in zeroAmountLines)
                        {
                            result.Lines.Remove(candidate);
                            _logger.LogDebug("[RECONCILE] Removed zero-amount line (conf={Conf}): {Desc}",
                                candidate.Confidence, candidate.Description);
                        }

                        // Recalculate diff after zero-amount removal
                        linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
                        diff = linesSumExcl - result.AmountExclVat;

                        // Step 2: If still over, remove lines where qty > 1000 (likely weight/volume)
                        if (diff > 0.05m)
                        {
                            var weightLines = result.Lines
                                .Where(l => l.Quantity.HasValue && l.Quantity.Value > 1000)
                                .ToList();
                            foreach (var wl in weightLines)
                            {
                                result.Lines.Remove(wl);
                                _logger.LogDebug("[RECONCILE] Removed large-qty line (likely weight): {Desc} qty={Qty}", wl.Description, wl.Quantity);
                            }
                        }

                        // Step 3: If still over, remove duplicate descriptions keeping the one closest to remaining diff
                        linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
                        diff = linesSumExcl - result.AmountExclVat;
                        if (diff > 0.05m)
                        {
                            var duplicateDescs = result.Lines
                                .GroupBy(l => l.Description)
                                .Where(g => g.Count() > 1)
                                .SelectMany(g => g.Skip(1))
                                .ToList();
                            foreach (var dl in duplicateDescs)
                            {
                                result.Lines.Remove(dl);
                                _logger.LogDebug("[RECONCILE] Removed duplicate description line: {Desc}", dl.Description);
                                linesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
                                if (Math.Abs(linesSumExcl - result.AmountExclVat) <= 0.05m) break;
                            }
                        }

                        _logger.LogDebug("[RECONCILE] After cleanup: LinesSumExcl={Sum} HeaderExcl={Header}",
                            result.Lines.Sum(l => l.AmountExclVat), result.AmountExclVat);
                    }

                    // CASE 2: Lines sum LESS than header (missing lines — e.g. Delamode with merged lines)
                    // Strategy: do nothing — let user add lines manually via edit dialog
                    // Just ensure AMOUNT_MISMATCH flag is set correctly (handled later)
                }

                _logger.LogDebug("[AZURE DI] supplier={Supplier} vat={Vat} inv={Invoice} total={Total}",
                    result.SupplierName, result.SupplierVatCode, result.InvoiceNumber, result.AmountInclVat);

                // Load company settings early for VIES own-company check
                var settings = await _companySettingsService.GetSettingsAsync();

                // VIES lookup
                if (!string.IsNullOrEmpty(result.SupplierVatCode))
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

                // Find supplier ID and DefaultExpenseCategoryId
                var (supplierId, defaultCategoryId) = await FindSupplierIdAsync(result.SupplierName, result.SupplierVatCode);
                result.SupplierId = supplierId;

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

                // LINES_NOT_FOUND: result.Lines.Count == 0
                if (result.Lines.Count == 0)
                    result.Flags.Add(OcrFlag.LinesNotFound);

                // AMOUNT_MISMATCH: result.Lines.Count > 0 && ExclVat diff >= 0.05
                // If ExclVat matches (diff < 0.05) but InclVat doesn't - do NOT set flag (InclVat diff is just VAT rounding)
                var mismatchLinesSumExcl = result.Lines.Sum(l => l.AmountExclVat);
                var diffExcl = Math.Abs(mismatchLinesSumExcl - result.AmountExclVat);
                _logger.LogDebug("[MISMATCH CHECK] LinesSumExcl={Lines} HeaderExcl={Header} Diff={Diff}", mismatchLinesSumExcl, result.AmountExclVat, diffExcl);

                var linesSumIncl = result.Lines.Sum(l => l.AmountInclVat);
                var diffIncl = Math.Abs(linesSumIncl - result.AmountInclVat);
                _logger.LogDebug("[MISMATCH CHECK] LinesSumIncl={Lines} HeaderIncl={Header} Diff={Diff}", linesSumIncl, result.AmountInclVat, diffIncl);

                if (result.Lines.Count > 0 && diffExcl >= 0.05m && diffIncl >= 0.05m)
                    result.Flags.Add(OcrFlag.AmountMismatch);

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

        public async Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName)
            => await ProcessAsync(base64, fileName);

        public async Task<(int? supplierId, int? defaultCategoryId)> FindSupplierIdAsync(string supplierName, string vatCode)
        {
            if (string.IsNullOrEmpty(vatCode) && string.IsNullOrEmpty(supplierName)) return (null, null);

            await using var context = _dbFactory.CreateDbContext();

            _logger.LogDebug("[FIND SUPPLIER] name='{Name}' vat='{Vat}'", supplierName, vatCode);

            // Try by VAT first, then fallback to name — return both Id and DefaultExpenseCategoryId
            int? supplierId = null;
            int? defaultCategoryId = null;

            var supplierByVat = await context.BusinessPartners
                .Where(bp => bp.VatCode == vatCode
                          || bp.VatCode == "LT" + vatCode
                          || bp.VatCode == vatCode.TrimStart('L', 'T'))
                .Select(bp => new { bp.Id, bp.DefaultExpenseCategoryId })
                .FirstOrDefaultAsync();

            if (supplierByVat != null && supplierByVat.Id > 0)
            {
                supplierId = supplierByVat.Id;
                defaultCategoryId = supplierByVat.DefaultExpenseCategoryId;
            }
            else if (!string.IsNullOrEmpty(supplierName))
            {
                // Fallback: try by supplier name (contains match)
                var supplierByName = await context.BusinessPartners
                    .Where(bp => bp.Name.Contains(supplierName))
                    .Select(bp => new { bp.Id, bp.DefaultExpenseCategoryId })
                    .FirstOrDefaultAsync();

                if (supplierByName != null && supplierByName.Id > 0)
                {
                    supplierId = supplierByName.Id;
                    defaultCategoryId = supplierByName.DefaultExpenseCategoryId;
                }
            }

            return (supplierId, defaultCategoryId);
        }

        private static int ToConfidencePercent(float? confidence) =>
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