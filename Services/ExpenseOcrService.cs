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
                        if (addrObj.TryGetProperty("countryRegion", out var country)) result.SupplierCountryCode = CountryCodeResolver.FromAddress(country.GetString()) ?? "";
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

                // Company (registration) code: NOT read here. The old four-step block filled SupplierCompanyCode with the
                // whole VAT code (prefix included) or with the address recipient's name; it is now extracted after the
                // own company's settings are loaded, so the buyer's codes can be excluded (Etapas 2 S2c, D-045).

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

                // Etapas 3 S3 (PLAN-ETAPAS3 §1 option (c), §8.1 S3, D-023): repair a line's net from
                // analyzeResult.tables[] when the table's rows reconcile against the header and the Items-derived
                // lines do not — before the locale-number detection below, so it judges the corrected value.
                var tableRepair = TableLineRepair.Repair(analyzeResult, result);
                if (tableRepair.Outcome == TableRepairOutcome.Repaired)
                    result.Flags.Add(OcrFlag.LinesRepairedFromTable);

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

                // Registration code: a number, or empty — never a VAT code, never a name (PLAN-ETAPAS2 §0.3, D-045). An invoice
                // prints both parties' codes, so our own codes and the buyer's VAT digits are excluded from the candidates.
                var companyCode = ExtractSupplierCompanyCode(root, settings, result.CustomerVatCode);
                result.SupplierCompanyCode = companyCode.Code;
                _logger.LogDebug("[COMPANY CODE] source={Source} found={Found}", companyCode.Source, companyCode.Code.Length > 0);

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
                // VENDOR_NOT_FOUND (+ VENDOR_AMBIGUOUS / VENDOR_SUGGESTED by the matcher's outcome): result.SupplierId == null
                ApplySupplierFlags(result);

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

                // ZERO_VAT / ZERO_VAT_NO_BASIS (D-026, PLAN-ETAPAS3 §5, D-046 OQ-4/OQ-5, Etapas 3 S4): same country
                // resolution as the VAT-rate whitelist below — the assigned partner's stored country once a supplier
                // exists, the document's own country otherwise (ExpenseService.ResolveRateCountry, not a second
                // mechanism). CJEU C-247/21: the check runs at extraction time (create/re-OCR), not only at approval.
                if (result.VatRate == 0 && result.AmountInclVat > 0)
                {
                    var rateCountry = ExpenseService.ResolveRateCountry(partnerCountry, result.SupplierCountryCode, supplierId.HasValue);
                    var documentText = analyzeResult.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
                        ? contentEl.GetString() : null;
                    var basis = ZeroVatFormulationExtractor.Check(documentText, rateCountry);
                    // Confirmed-and-found closes the flag entirely (D-026 "vėliavėlė užsidaro"); confirmed-and-not-found
                    // is a real, actionable review reason. Everything else (today: every country is UNCONFIRMED) keeps
                    // TODAY'S behaviour byte-for-byte unchanged — plain ZERO_VAT, nothing gets looser before a human
                    // confirms a country's list.
                    if (basis.Outcome == ZeroVatCheckOutcome.ConfirmedNoBasisFound)
                        result.Flags.Add(OcrFlag.ZeroVatNoBasis);
                    else if (basis.Outcome != ZeroVatCheckOutcome.BasisConfirmedFound)
                        result.Flags.Add(OcrFlag.ZeroVat);
                }

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

            // Country is an ISO alpha-2 code or empty, never a truncated name (D-042): a well-formed VAT prefix wins
            // over the address country
            result.SupplierCountryCode = CountryCodeResolver.Resolve(result.SupplierVatCode, result.SupplierCountryCode) ?? "";

            // Normalize company name
            var normalized = CompanyNameHelper.Normalize(result.SupplierName);
            if (!normalized.Equals(result.SupplierName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("[NAME NORMALIZE] '{Old}' -> '{New}'", result.SupplierName, normalized);
                result.SupplierName = normalized;
            }

            // The ONE supplier match of the pipeline (Etapas 2 S3, D-044): the upload dialog and the save paths use
            // result.SupplierId / result.SupplierMatch and never match again. A malformed VAT code never matches by VAT;
            // an IBAN never assigns on its own (it only checks / suggests); the company code is the extracted one.
            var document = SupplierMatching.Document(result.SupplierName, vatFormatWrong ? null : result.SupplierVatCode,
                result.SupplierCompanyCode, result.SupplierBankAccount, result.SupplierCountryCode);
            var (match, snapshot) = await SupplierMatching.MatchAsync(_dbFactory, document);
            _logger.LogDebug("[SUPPLIER MATCH] {Match}", SupplierMatching.DescribeForAudit(match));
            result.SupplierMatch = match;
            result.SupplierId = match.PartnerId;
            return snapshot.DefaultCategoryOf(match.PartnerId);
        }

        /// <summary>
        /// The supplier flags of an OCR result without a supplier: VENDOR_NOT_FOUND always, plus VENDOR_AMBIGUOUS when two or
        /// more partners tied and VENDOR_SUGGESTED when the matcher only has a weaker or contradicted candidate (D-044).
        /// Information flags — the invoice is PENDING_SUPPLIER because it has no supplier. For an assigned supplier only SUPPLIER_NEW_IBAN (review) can apply.
        /// </summary>
        public static void ApplySupplierFlags(OcrResultDto result)
        {
            void Add(string flag) { if (!result.Flags.Contains(flag)) result.Flags.Add(flag); }

            if (result.SupplierId != null)
            {
                // SUPPLIER_NEW_IBAN (D-044 Q5): only when the partner already has a known account and the document's valid IBAN
                // is not among them. The save paths recompute it from the stored data (ExpenseService), this is the preview.
                var match = result.SupplierMatch;
                if (match is { Outcome: MatchOutcome.Assigned, DocumentIbanKnown: false, PartnerKnownIbanCount: >= 1 })
                    Add(OcrFlag.SupplierNewIban);
                return;
            }
            Add(OcrFlag.VendorNotFound);
            switch (result.SupplierMatch?.Outcome)
            {
                case MatchOutcome.Ambiguous: Add(OcrFlag.VendorAmbiguous); break;
                case MatchOutcome.Suggested: Add(OcrFlag.VendorSuggested); break;
            }
        }

        public async Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName)
            => await ProcessAsync(base64, fileName);

        /// <summary>
        /// The supplier's registration code from a raw Azure response (Etapas 2 S2c, D-045): a number or empty, never a VAT code or
        /// a name. Reads <c>VendorBusinessNumber</c> and the document text; our own company code / VAT digits and the buyer's VAT
        /// digits are excluded because an invoice prints both parties' codes. See <see cref="SupplierCompanyCodeExtractor"/>.
        /// </summary>
        public static CompanyCodeExtraction ExtractSupplierCompanyCode(JsonElement root, CompanySettings settings, string? customerVatCode)
        {
            var resultRoot = root.TryGetProperty("analyzeResult", out var analyzeRoot) ? analyzeRoot : root;
            var documentText = resultRoot.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String
                ? contentEl.GetString() ?? "" : "";

            var businessNumber = "";
            if ((resultRoot.TryGetProperty("documents", out var documents) || resultRoot.TryGetProperty("Documents", out documents))
                && documents.ValueKind == JsonValueKind.Array && documents.GetArrayLength() > 0)
            {
                var doc = documents[0];
                var fields = doc.TryGetProperty("fields", out var f1) ? f1 : doc.TryGetProperty("Fields", out var f2) ? f2 : doc;
                if (fields.TryGetProperty("VendorBusinessNumber", out var field) && field.ValueKind != JsonValueKind.Null)
                    businessNumber = field.TryGetProperty("valueString", out var vs) ? vs.GetString() ?? "" :
                                     field.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
            }

            return SupplierCompanyCodeExtractor.Extract(businessNumber, documentText,
                new[] { settings.CompanyCode, settings.VatCode, customerVatCode });
        }

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

        /// <summary>
        /// Interface-compatible wrapper over the cascade (<see cref="SupplierMatching"/>): the partner id and its default
        /// category when the matcher would assign automatically, otherwise (null, null) — not found, ambiguous, suggested
        /// (a contradicting or weaker match) and ineligible partners all stay loud (D-017, D-044, D-045).
        /// </summary>
        public async Task<(int? supplierId, int? defaultCategoryId)> FindSupplierIdAsync(string supplierName, string vatCode)
        {
            var document = SupplierMatching.Document(supplierName, vatCode, null, null, null);
            var (match, snapshot) = await SupplierMatching.MatchAsync(_dbFactory, document);
            _logger.LogDebug("[FIND SUPPLIER] {Match}", SupplierMatching.DescribeForAudit(match));
            return match.Outcome == MatchOutcome.Assigned
                ? (match.PartnerId, snapshot.DefaultCategoryOf(match.PartnerId))
                : (null, null);
        }

        internal static int ToConfidencePercent(float? confidence) =>
            confidence.HasValue ? (int)Math.Round(confidence.Value * 100) : 0;

        private static string CleanVatCode(string raw)
        {
            // Remove spaces, dashes, dots that sometimes appear in extracted VAT codes
            return raw.Replace(" ", "").Replace("-", "").Replace(".", "").Trim();
        }

    }
}