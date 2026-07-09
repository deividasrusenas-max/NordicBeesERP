using System.Linq;
using NordicBeesERP.Models;

public class OcrResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }

    // Supplier data from Azure DI + VIES
    public string SupplierName { get; set; } = "";
    public string SupplierVatCode { get; set; } = "";
    public string SupplierCompanyCode { get; set; } = "";
    public string SupplierAddress { get; set; } = "";
    public string SupplierCity { get; set; } = "";
    public string SupplierPostalCode { get; set; } = "";
    public string SupplierCountryCode { get; set; } = "";
    public string SupplierBankAccount { get; set; } = "";
    public string SupplierPhone { get; set; } = "";
    public string SupplierEmail { get; set; } = "";

    // VIES verification
    public bool ViesVerified { get; set; }
    public string? ViesName { get; set; }
    public string? ViesAddress { get; set; }
    public bool ViesServiceAvailable { get; set; } = true;

    // Invoice header
    public string InvoiceNumber { get; set; } = "";
    public string InvoiceDate { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Currency { get; set; } = PdfLocalization.CurrencyCode;
    public decimal AmountExclVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal AmountInclVat { get; set; }

    // Customer (buyer) validation
    public string CustomerName { get; set; } = "";
    public string CustomerVatCode { get; set; } = "";

    // Lines
    public List<OcrLineDto> Lines { get; set; } = new();

    // Validation
    public int? SupplierId { get; set; }
    public string? PendingSupplierName { get; set; }
    public int? CategoryId { get; set; }
    public List<string> Flags { get; set; } = new();
    public bool LinesMatchHeader { get; set; } = true;

    // File info (set after file is saved to disk)
    public string? OriginalFilePath { get; set; }
    public string? OriginalFilename { get; set; }

    // Metadata
    public OcrConfidenceDto Confidence { get; set; } = new();
    public string OcrPipeline { get; set; } = "AZURE_DI";

    // Azure DI diagnostics
    public OcrDiagnosticsDto Diagnostics { get; set; } = new();
}

public class OcrLineDto
{
    public string Description { get; set; } = "";
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal AmountExclVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal AmountInclVat { get; set; }
    public int? SuggestedCategoryId { get; set; }
    public decimal Confidence { get; set; } = 1.0m;
}

public class OcrConfidenceDto
{
    public int SupplierName { get; set; }
    public int InvoiceNumber { get; set; }
    public int InvoiceDate { get; set; }
    public int DueDate { get; set; }
    public int Amounts { get; set; }

    // Weighted average - only non-zero critical fields
    // Weights: Amounts=30%, InvoiceNumber=25%, SupplierName=25%, InvoiceDate=20%
    // DueDate is excluded - it is a bonus field
    public int Overall
    {
        get
        {
            var weighted = new (int value, int weight)[]
            {
                (Amounts, 30),
                (InvoiceNumber, 25),
                (SupplierName, 25),
                (InvoiceDate, 20)
            };
            var relevant = weighted.Where(x => x.value > 0).ToList();
            if (!relevant.Any()) return 0;
            var totalWeight = relevant.Sum(x => x.weight);
            var sum = relevant.Sum(x => x.value * x.weight);
            return sum / totalWeight;
        }
    }
}

public class OcrDiagnosticsDto
{
    public bool? AzureReachable { get; set; }
    public string? AzureError { get; set; }
}

public static class OcrFlag
{
    public const string VendorNotFound    = "VENDOR_NOT_FOUND";
    public const string WrongRecipient    = "WRONG_RECIPIENT";
    public const string MissingAmount     = "MISSING_AMOUNT";
    public const string MissingInvNumber  = "MISSING_INV_NUMBER";
    public const string MissingDueDate    = "MISSING_DUE_DATE";
    public const string ZeroVat           = "ZERO_VAT";
    public const string LinesNotFound     = "LINES_NOT_FOUND";
    public const string AmountMismatch    = "AMOUNT_MISMATCH";
    public const string LowConfidence     = "LOW_CONFIDENCE";
    public const string Duplicate         = "DUPLICATE";
    public const string ViesUnavailable   = "VIES_UNAVAILABLE";
    public const string AzureLimit        = "AZURE_LIMIT";
    public const string OwnCompany        = "OWN_COMPANY";
}
