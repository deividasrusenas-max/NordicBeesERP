using System.Linq;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Validation;

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

    // The printed text next to Azure's typed header totals — kept for locale-number detection only (D-041), never persisted.
    public OcrNumberRead? SubTotalRead { get; set; }
    public OcrNumberRead? TotalTaxRead { get; set; }
    public OcrNumberRead? InvoiceTotalRead { get; set; }

    // Customer (buyer) validation
    public string CustomerName { get; set; } = "";
    public string CustomerVatCode { get; set; } = "";

    // Lines
    public List<OcrLineDto> Lines { get; set; } = new();

    // Validation
    public int? SupplierId { get; set; }
    // What the supplier cascade decided (Etapas 2 S3): the one match the upload dialog and the save paths use. Null when no
    // match ran (an invoice built by hand); audited as SUPPLIER_MATCHED on create and re-OCR.
    public SupplierMatch? SupplierMatch { get; set; }
    public string? PendingSupplierName { get; set; }
    public int? CategoryId { get; set; }
    public List<string> Flags { get; set; } = new();
    public bool LinesMatchHeader { get; set; } = true;

    // File info (set after file is saved to disk)
    public string? OriginalFilePath { get; set; }
    public string? OriginalFilename { get; set; }
    public long? FileId { get; set; }

    // Metadata
    public OcrConfidenceDto Confidence { get; set; } = new();
    public string OcrPipeline { get; set; } = "AZURE_DI";

    // Raw Azure DI analyze response (persisted to expense_invoices.ocr_raw_json)
    public string? RawJson { get; set; }

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

    // The document gave no line amount: AmountExclVat was computed as UnitPrice × Quantity. Such a line is
    // left out of the line rule (it would only compare the number with itself) but stays in BR-CO-10.
    public bool NetDerived { get; set; }

    // The printed text next to Azure's typed quantity / unit price / line amount — locale-number detection only
    // (D-041), never persisted. A derived net (NetDerived) has no AmountRead.
    public OcrNumberRead? QuantityRead { get; set; }
    public OcrNumberRead? UnitPriceRead { get; set; }
    public OcrNumberRead? AmountRead { get; set; }
}

/// <summary>
/// One numeric field as Azure returned it: <see cref="Printed"/> is the document text (<c>content</c>),
/// <see cref="Typed"/> Azure's typed value, <see cref="Stored"/> what was put into the DTO (header totals are rounded
/// to cents). A DTO value that differs from <see cref="Stored"/> was edited by a human — that is the resolution of a flag.
/// </summary>
public sealed record OcrNumberRead(string Printed, decimal Typed, decimal Stored);

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
    public const string InvalidVatRate    = "INVALID_VAT_RATE";
    public const string AmountArithmeticMismatch = "AMOUNT_ARITHMETIC_MISMATCH";
    public const string MissingMoneyField        = "MISSING_MONEY_FIELD";
    public const string FutureDate               = "FUTURE_DATE";
    public const string StaleDate                = "STALE_DATE";
    public const string MissingInvDate           = "MISSING_INV_DATE";

    // OCR Etapas 1 (PLAN-ETAPAS1 §1.3, §2, §3; D-038). Declared in S2; nothing sets them yet.
    // Review (hold the invoice in NEEDS_REVIEW):
    public const string TotalsOutOfRange         = "TOTALS_OUT_OF_RANGE";
    public const string InvalidIban              = "INVALID_IBAN";         // information once a supplier is assigned (D-038 Q5)
    public const string InvalidVatFormat         = "INVALID_VAT_FORMAT";   // information once a supplier is assigned (D-038 Q5)
    public const string VatRateNotAllowed        = "VAT_RATE_NOT_ALLOWED";
    public const string NumberMisread            = "NUMBER_MISREAD";
    public const string NumberAmbiguous          = "NUMBER_AMBIGUOUS";
    // Information only (D-038 Q3, Q4):
    public const string LineAmountImplausible    = "LINE_AMOUNT_IMPLAUSIBLE";
    public const string VatFormatUnchecked       = "VAT_FORMAT_UNCHECKED";
    public const string VatRateUnchecked         = "VAT_RATE_UNCHECKED";
    public const string VatCountryMismatch       = "VAT_COUNTRY_MISMATCH"; // well-formed VAT code whose prefix differs from the supplier's country (S5)
    public const string LineSumRounding          = "LINE_SUM_ROUNDING";    // BR-CO-10 difference > 0 and ≤ 0.05 € (D-040)

    // S7 (D-041, D-038 Q7): the reconcile step no longer deletes lines; where it used to, the line is kept and marked.
    // Information only — BR-CO-10 already holds the invoice in review in that situation.
    public const string LineLargeQuantity        = "LINE_LARGE_QUANTITY";        // a line with quantity > 1000 while lines exceed the header
    public const string LineDuplicateDescription = "LINE_DUPLICATE_DESCRIPTION"; // repeated description while lines exceed the header

    // Etapas 2 S1 (D-044 Q6): re-OCR found a different partner than the one a human assigned. The assigned
    // supplier is kept; the fresh match is shown as a suggestion. Information only — never a review flag.
    public const string VendorSuggested          = "VENDOR_SUGGESTED";

    // Etapas 2 S3 (D-044): two or more partners tie at the deciding tier — nothing is assigned, the candidates are shown.
    // Information only; the invoice stays PENDING_SUPPLIER because there is no supplier.
    public const string VendorAmbiguous          = "VENDOR_AMBIGUOUS";

    // Etapas 2 S4 (D-044 Q5): the supplier already has at least one known IBAN and the document's valid IBAN is not among them.
    // REVIEW — and, unlike INVALID_IBAN, it stays review with a supplier (not covered by the D-039 item 2 exemption).
    public const string SupplierNewIban          = "SUPPLIER_NEW_IBAN";

    // Etapas 3 S3 (PLAN-ETAPAS3 §1 option (c), §8.1 S3): TableLineRepair replaced one or more lines' net amount from
    // analyzeResult.tables[] because the table's rows reconciled against the header where Items did not (D-023's
    // ASF0021438/EGO/UTA PL class). Information only — not in HasReviewFlag's list, and not in ManualEditOwnedFlags,
    // so it is carried over unchanged by an edit (same default-carry-over rule as any other document-derived flag).
    public const string LinesRepairedFromTable   = "LINES_REPAIRED_FROM_TABLE"; // "Eilutės pataisytos pagal lentelę"
}
