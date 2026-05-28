# NordicBeesERP — Išlaidų modulio perrašymo prompt'ai
# Vykdyti EILĖS TVARKA. Kiekvienas prompt'as yra atskira Cline sesija.
# Prieš kiekvieną prompt'ą perskaityti .clinerules/nordicbees-standards.md

---

## PROMPT 1 — DB migracija + modelių atnaujinimas

```
Read .clinerules/nordicbees-standards.md section 5 (DB SUDERINAMUMAS).

Using MySQL MCP, run these SQL statements one by one and verify each succeeds:

ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS category_id INT NULL AFTER invoice_type;
ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS approved_by VARCHAR(100) NULL AFTER notes;
ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS approved_at DATETIME NULL AFTER approved_by;
ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS rejected_reason VARCHAR(500) NULL AFTER approved_at;
ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS source ENUM('MANUAL','EMAIL','N8N') DEFAULT 'MANUAL' AFTER rejected_reason;
ALTER TABLE expense_invoices ADD COLUMN IF NOT EXISTS original_filename VARCHAR(255) NULL AFTER source;
ALTER TABLE expense_invoice_lines ADD COLUMN IF NOT EXISTS category_id INT NULL AFTER invoice_id;
ALTER TABLE expense_invoice_lines ADD COLUMN IF NOT EXISTS unit_of_measure VARCHAR(20) NULL AFTER unit_price;
ALTER TABLE expense_invoice_audit MODIFY COLUMN old_status VARCHAR(30) NULL;
ALTER TABLE expense_invoice_audit MODIFY COLUMN new_status VARCHAR(30) NULL;

After DB changes, update Models/Expenses/ExpenseInvoice.cs.

In ExpenseInvoice class add these properties (after SupplierVatVerifiedName):
    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("approved_by")]
    [MaxLength(100)]
    public string? ApprovedBy { get; set; }

    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

    [Column("rejected_reason")]
    [MaxLength(500)]
    public string? RejectedReason { get; set; }

    [Column("source")]
    [MaxLength(10)]
    public string Source { get; set; } = "MANUAL";

    [Column("original_filename")]
    [MaxLength(255)]
    public string? OriginalFilename { get; set; }

    [Column("ocr_pipeline")]
    [MaxLength(50)]
    public string? OcrPipeline { get; set; }

Also change ocr_confidence type from decimal? to int?:
    [Column("ocr_confidence")]
    public int? OcrConfidence { get; set; }

In ExpenseInvoiceLine class add:
    [Column("category_id")]
    public int? CategoryId { get; set; }

    [Column("unit_of_measure")]
    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

Update Models/Expenses/ExpenseModels.cs - in ExpenseInvoiceAudit class:
    Change [MaxLength(20)] to [MaxLength(30)] on OldStatus property
    Change [MaxLength(20)] to [MaxLength(30)] on NewStatus property

Run: dotnet build 2>&1 | grep "error CS"
Commit: "feat: add category, approval, source, pipeline columns to expense tables and models"
```

---

## PROMPT 2 — OcrResultDto + OcrFlag perrašymas

```
Read .clinerules/nordicbees-standards.md section 8 (OCR CONFIDENCE).

Rewrite Services/Dtos/OcrResultDto.cs completely with this exact content:

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
    public string Currency { get; set; } = "EUR";
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
    public List<string> Flags { get; set; } = new();
    public bool LinesMatchHeader { get; set; } = true;

    // File info (set after file is saved to disk)
    public string? OriginalFilePath { get; set; }
    public string? OriginalFilename { get; set; }

    // Metadata
    public OcrConfidenceDto Confidence { get; set; } = new();
    public string OcrPipeline { get; set; } = "AZURE_DI";
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
}

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite OcrResultDto with weighted confidence, buyer validation, file fields"
```

---

## PROMPT 3 — IExpenseOcrService + ExpenseOcrService perrašymas

```
Read .clinerules/nordicbees-standards.md sections 4, 20.
Read Services/Dtos/OcrResultDto.cs.
Read Services/ViesService.cs (has 15s timeout - do not change).
Read Services/ICompanySettingsService.cs.

CRITICAL: OcrQueueWorker calls ExtractInvoiceDataAsync on IExpenseOcrService.
Must keep this method to avoid breaking OcrQueueWorker.

STEP 1: Rewrite Services/IExpenseOcrService.cs:

public interface IExpenseOcrService
{
    // Primary method used by ExpenseUploadDialog
    Task<OcrResultDto> ProcessAsync(string base64, string fileName);

    // Kept for OcrQueueWorker backward compatibility - delegates to ProcessAsync
    Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName);

    Task<int?> FindSupplierIdAsync(string supplierName, string vatCode);
    Task<bool> IsAzureHealthyAsync();
}

STEP 2: Rewrite Services/ExpenseOcrService.cs completely.

Constructor: inject IDbContextFactory<NordicBeesERPContext>, IViesService,
             ICompanySettingsService, ILogger<ExpenseOcrService>

Implement ExtractInvoiceDataAsync as simple alias:
    public async Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName)
        => await ProcessAsync(base64, fileName);

ProcessAsync(string base64, string fileName):
  1. Get Azure credentials from app_settings table (keys: azure_di_endpoint, azure_di_key)
  2. Call Azure DI prebuilt-invoice model with base64
  3. Handle HTTP 429 → add OcrFlag.AzureLimit, return result with Success=false, ErrorMessage set
  4. Extract from Azure DI response:
     - VendorName → SupplierName
     - VendorTaxId → SupplierVatCode (clean via CleanVatCode helper)
     - VendorAddress.valueAddress: streetAddress→SupplierAddress, city→SupplierCity,
       postalCode→SupplierPostalCode, countryRegion→SupplierCountryCode (normalize via NormalizeCountryCode)
     - PaymentDetails[0]: IBAN or AccountNumber → SupplierBankAccount
     - InvoiceId → InvoiceNumber
     - InvoiceDate (valueDate) → InvoiceDate string
     - DueDate (valueDate) → DueDate string
     - SubTotal → AmountExclVat
     - TotalTax → VatAmount
     - InvoiceTotal → AmountInclVat
     - TaxRate → VatRate (first non-zero from items or header)
     - Items[] → Lines list
     - CustomerName → CustomerName
     - CustomerId or CustomerTaxId → CustomerVatCode
     - Confidence values → OcrConfidenceDto fields (0-100 int)
  5. CountryCode normalization:
     private static string NormalizeCountryCode(string value)
     {
         if (string.IsNullOrEmpty(value)) return "";
         if (value.Length == 2) return value.ToUpper();
         try { return new System.Globalization.RegionInfo(value).TwoLetterISORegionName; }
         catch { return value.Length >= 2 ? value[..2].ToUpper() : value.ToUpper(); }
     }
  6. If SupplierVatCode not empty: call ViesService.LookupAsync()
     - If !ServiceAvailable → add OcrFlag.ViesUnavailable to flags, continue without VIES
     - If IsValid && Name not null → CompanyNameHelper.Normalize(viesResult.Name) → override SupplierName
     - Set result.ViesVerified = isValid, ViesName, ViesAddress
  7. If SupplierCountryCode empty: take first 2 chars of SupplierVatCode as fallback
  8. Call FindSupplierIdAsync(SupplierName, SupplierVatCode) → result.SupplierId
  9. Get company settings - NOT hardcoded:
     var settings = await _companySettingsService.GetSettingsAsync();
  10. Build flags in this order:
      VENDOR_NOT_FOUND:   result.SupplierId == null
      WRONG_RECIPIENT:    !string.IsNullOrEmpty(result.CustomerVatCode)
                          && result.CustomerVatCode != settings.VatCode
                          && !result.CustomerName.Contains(settings.CompanyName,
                             StringComparison.OrdinalIgnoreCase)
      MISSING_AMOUNT:     result.AmountInclVat == 0
      MISSING_INV_NUMBER: string.IsNullOrEmpty(result.InvoiceNumber)
      MISSING_DUE_DATE:   string.IsNullOrEmpty(result.DueDate)
      ZERO_VAT:           result.VatRate == 0 && result.AmountInclVat > 0
      LINES_NOT_FOUND:    result.Lines.Count == 0
      AMOUNT_MISMATCH:    result.Lines.Count > 0 &&
                          Math.Abs(result.Lines.Sum(l => l.AmountInclVat) - result.AmountInclVat) > 0.01m
      LOW_CONFIDENCE:     result.Confidence.Overall > 0 && result.Confidence.Overall < 50
  11. result.LinesMatchHeader = !result.Flags.Contains(OcrFlag.AmountMismatch)
  12. result.OcrPipeline = "AZURE_DI"
  13. result.Success = true always (even with flags - OCR completed)
      result.Success = false ONLY on unhandled exception or HTTP 429

FindSupplierIdAsync:
  1. If vatCode not empty: find by exact vat_code match → return Id
  2. If name not empty: find by name Contains → return Id
  3. Return null if not found

IsAzureHealthyAsync: simple HTTP GET to Azure endpoint, return true/false

Keep CleanVatCode() helper (removes spaces, uppercase).

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite ExpenseOcrService - no hardcoded values, ExtractInvoiceDataAsync alias, full flags"
```

---

## PROMPT 4 — IExpenseService + ExpenseService papildymas

```
Read .clinerules/nordicbees-standards.md sections 6 (AUDIT LOG), 20.
Read Services/IExpenseService.cs.
Read Services/Dtos/OcrResultDto.cs.
Read Models/Expenses/ExpenseInvoice.cs.

STEP 1: Update Services/IExpenseService.cs - ADD these methods (keep ALL existing):

    Task<ExpenseInvoice> CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL");
    Task<int?> CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat);
    Task AssignSupplierAsync(int invoiceId, int supplierId, string performedBy);
    Task ApproveAsync(int invoiceId, string performedBy);
    Task RejectAsync(int invoiceId, string reason, string performedBy);

REMOVE old signatures:
    Task<bool> CheckDuplicateAsync(int supplierId, string invoiceNumber, DateTime invoiceDate, decimal amount);
    Task AssignSupplierAsync(int invoiceId, int supplierId);

STEP 2: Add IAuthService to ExpenseService constructor:
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IAuthService _authService;
    public ExpenseService(IDbContextFactory<NordicBeesERPContext> dbFactory, IAuthService authService)
    { _dbFactory = dbFactory; _authService = authService; }

STEP 3: Implement CreateFromOcrAsync in ExpenseService:

    public async Task<ExpenseInvoice> CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL")
    {
        var currentUser = await _authService.GetAuthenticatedUserAsync();
        var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "system";

        string status;
        if (ocrResult.Flags.Contains(OcrFlag.WrongRecipient))
            status = "REJECTED";
        else if (ocrResult.SupplierId == null)
            status = "PENDING_SUPPLIER";
        else if (ocrResult.Flags.Any(f => f == OcrFlag.MissingAmount || f == OcrFlag.AmountMismatch ||
                                          f == OcrFlag.LowConfidence || f == OcrFlag.ZeroVat ||
                                          f == OcrFlag.MissingInvNumber))
            status = "NEEDS_REVIEW";
        else
            status = "PENDING";

        var duplicateId = await CheckDuplicateAsync(ocrResult.SupplierId, ocrResult.SupplierVatCode,
            ocrResult.InvoiceNumber, ocrResult.AmountInclVat);
        if (duplicateId.HasValue)
        {
            if (!ocrResult.Flags.Contains(OcrFlag.Duplicate)) ocrResult.Flags.Add(OcrFlag.Duplicate);
            status = "DUPLICATE_PENDING";
        }

        DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate);
        if (invoiceDate == default) invoiceDate = DateTime.Today;
        DateTime.TryParse(ocrResult.DueDate, out var dueDate);
        if (dueDate == default) dueDate = invoiceDate.AddDays(30);

        await using var ctx = _dbFactory.CreateDbContext();

        var invoice = new ExpenseInvoice
        {
            SupplierId = ocrResult.SupplierId,
            InvoiceType = "STANDARD",
            Source = source,
            OriginalFilePath = ocrResult.OriginalFilePath,
            OriginalFilename = ocrResult.OriginalFilename,
            PendingSupplierName = ocrResult.SupplierId == null ? ocrResult.SupplierName : null,
            PendingSupplierVat = ocrResult.SupplierId == null ? ocrResult.SupplierVatCode : null,
            PendingSupplierAddress = ocrResult.SupplierId == null ? ocrResult.SupplierAddress : null,
            PendingSupplierCity = ocrResult.SupplierId == null ? ocrResult.SupplierCity : null,
            PendingSupplierPostalCode = ocrResult.SupplierId == null ? ocrResult.SupplierPostalCode : null,
            PendingSupplierCountryCode = ocrResult.SupplierId == null ? ocrResult.SupplierCountryCode : null,
            PendingSupplierCompanyCode = ocrResult.SupplierId == null ? ocrResult.SupplierCompanyCode : null,
            PendingSupplierBankAccount = ocrResult.SupplierId == null ? ocrResult.SupplierBankAccount : null,
            InvoiceNumber = ocrResult.InvoiceNumber,
            InvoiceDate = invoiceDate,
            DueDate = dueDate,
            AmountExclVat = ocrResult.AmountExclVat,
            VatRate = ocrResult.VatRate,
            VatAmount = ocrResult.VatAmount,
            AmountInclVat = ocrResult.AmountInclVat,
            PaidAmount = 0,
            Currency = string.IsNullOrEmpty(ocrResult.Currency) ? "EUR" : ocrResult.Currency,
            Status = status,
            OcrStatus = "COMPLETED",
            OcrConfidence = ocrResult.Confidence.Overall,
            OcrPipeline = ocrResult.OcrPipeline,
            OcrFlags = ocrResult.Flags.Any() ? System.Text.Json.JsonSerializer.Serialize(ocrResult.Flags) : null,
            SupplierVatVerified = ocrResult.ViesVerified,
            SupplierVatVerifiedName = ocrResult.ViesName,
            RejectedReason = status == "REJECTED" ? "Sąskaita ne MB Lakštenai" : null,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        ctx.ExpenseInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        for (int i = 0; i < ocrResult.Lines.Count; i++)
        {
            var line = ocrResult.Lines[i];
            ctx.ExpenseInvoiceLines.Add(new ExpenseInvoiceLine
            {
                InvoiceId = invoice.Id,
                Description = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.UnitOfMeasure,
                AmountExclVat = line.AmountExclVat,
                VatRate = line.VatRate,
                AmountInclVat = line.AmountInclVat,
                CategoryId = line.SuggestedCategoryId,
                SortOrder = i + 1
            });
        }
        if (ocrResult.Lines.Any()) await ctx.SaveChangesAsync();

        ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.InvoiceNumber,
            Action = "CREATED",
            ActionDetails = $"Šaltinis: {source}, tikslumas: {ocrResult.Confidence.Overall}%, požymiai: {string.Join(", ", ocrResult.Flags)}",
            OldStatus = null,
            NewStatus = status,
            PerformedBy = performedBy,
            PerformedAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();

        return invoice;
    }

STEP 4: Replace CheckDuplicateAsync:
    public async Task<int?> CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat)
    {
        if (string.IsNullOrEmpty(invoiceNumber)) return null;
        await using var ctx = _dbFactory.CreateDbContext();
        var query = ctx.ExpenseInvoices.Where(e =>
            e.InvoiceNumber == invoiceNumber && e.InvoiceNumber != "" &&
            e.Status != "REJECTED" && e.Status != "DUPLICATE_PENDING" &&
            Math.Abs(e.AmountInclVat - amountInclVat) < 0.01m);
        if (supplierId.HasValue)
            query = query.Where(e => e.SupplierId == supplierId.Value);
        else if (!string.IsNullOrEmpty(supplierVatCode))
            query = query.Where(e => e.SupplierId == null && e.PendingSupplierVat == supplierVatCode);
        else return null;
        var id = await query.Select(e => e.Id).FirstOrDefaultAsync();
        return id > 0 ? id : null;
    }

STEP 5: Replace AssignSupplierAsync:
    public async Task AssignSupplierAsync(int invoiceId, int supplierId, string performedBy)
    {
        await using var ctx = _dbFactory.CreateDbContext();
        var invoice = await ctx.ExpenseInvoices.FindAsync(invoiceId);
        if (invoice == null) return;
        var oldStatus = invoice.Status;
        invoice.SupplierId = supplierId;
        invoice.Status = "PENDING";
        invoice.UpdatedAt = DateTime.Now;
        ctx.Update(invoice);
        ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
        {
            InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
            Action = "SUPPLIER_ASSIGNED", ActionDetails = $"Tiekėjo ID: {supplierId}",
            OldStatus = oldStatus, NewStatus = "PENDING",
            PerformedBy = performedBy, PerformedAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();
    }

STEP 6: Add ApproveAsync:
    public async Task ApproveAsync(int invoiceId, string performedBy)
    {
        await using var ctx = _dbFactory.CreateDbContext();
        var invoice = await ctx.ExpenseInvoices.FindAsync(invoiceId);
        if (invoice == null) return;
        var oldStatus = invoice.Status;
        invoice.Status = invoice.PaidAmount >= invoice.AmountInclVat ? "APPROVED_PAID" : "APPROVED";
        invoice.ApprovedBy = performedBy;
        invoice.ApprovedAt = DateTime.Now;
        invoice.UpdatedAt = DateTime.Now;
        ctx.Update(invoice);
        ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
        {
            InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
            Action = "APPROVED", OldStatus = oldStatus, NewStatus = invoice.Status,
            PerformedBy = performedBy, PerformedAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();
    }

STEP 7: Add RejectAsync:
    public async Task RejectAsync(int invoiceId, string reason, string performedBy)
    {
        await using var ctx = _dbFactory.CreateDbContext();
        var invoice = await ctx.ExpenseInvoices.FindAsync(invoiceId);
        if (invoice == null) return;
        var oldStatus = invoice.Status;
        invoice.Status = "REJECTED";
        invoice.RejectedReason = reason;
        invoice.UpdatedAt = DateTime.Now;
        ctx.Update(invoice);
        ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
        {
            InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
            Action = "REJECTED", ActionDetails = reason,
            OldStatus = oldStatus, NewStatus = "REJECTED",
            PerformedBy = performedBy, PerformedAt = DateTime.Now
        });
        await ctx.SaveChangesAsync();
    }

IMPORTANT: Search for all callers of OLD signatures and update:
- CheckDuplicateAsync(supplierId, invoiceNumber, invoiceDate, amount) → new signature
- AssignSupplierAsync(invoiceId, supplierId) → add performedBy = "system" as placeholder

Run: dotnet build 2>&1 | grep "error CS"
Commit: "feat: CreateFromOcrAsync, ApproveAsync, RejectAsync, fix signatures"
```

---

## PROMPT 5 — ExpenseStatusHelper.cs perrašymas

```
Read .clinerules/nordicbees-standards.md section 18.
Read Services/Dtos/OcrResultDto.cs (OcrFlag constants).

Rewrite Helpers/ExpenseStatusHelper.cs completely:

public static class ExpenseStatusHelper
{
    public static string GetLabel(string? status) => status switch
    {
        "PENDING"           => "Laukia apmokėjimo",
        "PENDING_SUPPLIER"  => "Nežinomas tiekėjas",
        "NEEDS_REVIEW"      => "Reikia patikrinti",
        "DUPLICATE_PENDING" => "Dublikatas",
        "REJECTED"          => "Atmesta",
        "PARTIAL"           => "Dalinai apmokėta",
        "PAID"              => "Apmokėta",
        "APPROVED"          => "Patvirtinta",
        "APPROVED_PAID"     => "Patvirtinta ir apmokėta",
        _                   => status ?? "Nežinoma"
    };

    public static Color GetColor(string? status) => status switch
    {
        "PENDING"           => Color.Warning,
        "PENDING_SUPPLIER"  => Color.Error,
        "NEEDS_REVIEW"      => Color.Warning,
        "DUPLICATE_PENDING" => Color.Error,
        "REJECTED"          => Color.Dark,
        "PARTIAL"           => Color.Info,
        "PAID"              => Color.Success,
        "APPROVED"          => Color.Tertiary,
        "APPROVED_PAID"     => Color.Success,
        _                   => Color.Default
    };

    public static List<string> ParseFlags(string? ocrFlags)
    {
        if (string.IsNullOrWhiteSpace(ocrFlags)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ocrFlags) ?? new(); }
        catch { return new(); }
    }

    public static string GetFlagLabel(string flag) => flag switch
    {
        "VENDOR_NOT_FOUND"   => "Nežinomas tiekėjas",
        "WRONG_RECIPIENT"    => "Ne MB Lakštenai",
        "MISSING_AMOUNT"     => "Trūksta sumos",
        "MISSING_INV_NUMBER" => "Trūksta numerio",
        "MISSING_DUE_DATE"   => "Trūksta termino",
        "ZERO_VAT"           => "PVM = 0%",
        "LINES_NOT_FOUND"    => "Eilutės nerastos",
        "AMOUNT_MISMATCH"    => "Sumos nesutampa",
        "LOW_CONFIDENCE"     => "Žemas tikslumas",
        "DUPLICATE"          => "Dublikatas",
        "VIES_UNAVAILABLE"   => "VIES nepasiekiamas",
        "AZURE_LIMIT"        => "Azure limitas viršytas",
        _                    => flag
    };

    public static Color GetFlagColor(string flag) => flag switch
    {
        "VENDOR_NOT_FOUND"   => Color.Error,
        "WRONG_RECIPIENT"    => Color.Error,
        "AMOUNT_MISMATCH"    => Color.Error,
        "DUPLICATE"          => Color.Error,
        "AZURE_LIMIT"        => Color.Error,
        "MISSING_AMOUNT"     => Color.Warning,
        "MISSING_INV_NUMBER" => Color.Warning,
        "ZERO_VAT"           => Color.Warning,
        "LOW_CONFIDENCE"     => Color.Warning,
        "MISSING_DUE_DATE"   => Color.Default,
        "LINES_NOT_FOUND"    => Color.Default,
        "VIES_UNAVAILABLE"   => Color.Default,
        _                    => Color.Default
    };

    public static bool NeedsAttention(string? status) =>
        status is "PENDING_SUPPLIER" or "NEEDS_REVIEW" or "DUPLICATE_PENDING" or "REJECTED";

    public static bool IsCriticalFlag(string flag) =>
        flag is "VENDOR_NOT_FOUND" or "WRONG_RECIPIENT" or "AMOUNT_MISMATCH" or "DUPLICATE";
}

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite ExpenseStatusHelper"
```

---

## PROMPT 6 — ExpenseUploadDialog.razor perrašymas

```
Read .clinerules/nordicbees-standards.md (ALL sections).
Read Services/IExpenseOcrService.cs.
Read Services/IExpenseService.cs.
Read Services/Dtos/OcrResultDto.cs.
Read Helpers/ExpenseStatusHelper.cs.

Rewrite Components/Dialogs/ExpenseUploadDialog.razor completely.
UI ONLY — no business logic, no direct DB access.

Inject: IExpenseOcrService ExpenseOcrService, IExpenseService ExpenseService, ISnackbar Snackbar
CascadingParameter: IMudDialogInstance MudDialog

Phases: "upload" | "processing" | "review" | "error"

PHASE "upload":
  Inline title "Įkelti sąskaitą-faktūrą" (no TitleContent - wizard style)
  MudFileUpload: accept=".pdf", MaximumFileCount=1
    If _file == null: dashed border, CloudUpload icon, "Pasirinkite arba nutempkite PDF failą"
    If _file != null: green paper, CheckCircle icon, _file.Name
  "Analizuoti" MudButton Filled Primary, disabled when _file==null, OnClick=AnalyzeAsync
  DialogActions: [Atšaukti Text/Default]

PHASE "processing":
  Centered "Apdorojama..." MudText h6
  4 steps in a row (use div flex row):
    Each step: circle icon + label below
    Step 0 "Įkeltas": always green CheckCircle
    Step 1 "Skaitoma": blue pulsing when _processingStep==1, else gray
    Step 2 "AI analizė": blue pulsing when _processingStep==2, else gray
    Step 3 "Baigta": green CheckCircle when _processingStep==3, else gray
    Lines between steps: green if step before is done, gray otherwise
  CSS in <style> tag:
    .pulsing { animation: pulse 1.5s ease-in-out infinite; }
    @@keyframes pulse { 0%,100%{opacity:1;transform:scale(1)} 50%{opacity:0.6;transform:scale(0.9)} }
  MudProgressLinear Indeterminate Color=Primary Rounded at bottom

PHASE "review":
  Confidence chip: green(>=70) / yellow(>=50) / red(<50)
    "Patikimumas @_ocrResult.Confidence.Overall%"

  If WRONG_RECIPIENT in flags:
    MudAlert Severity.Error "Sąskaita ne MB Lakštenai — patikrinkite prieš išsaugant"

  If flags.Any():
    d-flex flex-wrap gap-1:
      foreach flag: MudChip Size.Small Variant.Outlined Color=GetFlagColor(flag): GetFlagLabel(flag)

  VIES section:
    If ViesVerified: yellow paper, CheckCircle green, "VIES patvirtinta: {ViesName}"
    Else if SupplierVatCode not empty: blue paper, "VIES nepatvirtinta"

  Tiekėjas section (blue paper):
    If SupplierId != null: CheckCircle green + "{SupplierName} rastas sistemoje"
    Else: Warning icon + "Tiekėjas '{SupplierName}' nerastas — bus priskirtas vėliau"

  Sąskaitos duomenys (blue paper):
    Row 1: Sąskaitos tipas (MudSelect: Standartinė/ULAK) | Nr. (readonly MudTextField)
    Row 2: Data (readonly) | Terminas (readonly)
    Row 3: Be PVM | PVM% | PVM suma
    Row 4: Suma su PVM (bold, larger text, blue color)

  Eilutės (only if Lines.Count > 0, blue paper):
    MudSimpleTable Dense:
      Th: Aprašymas | Kiekis | Be PVM | PVM% | Su PVM
      Tr foreach line
      Tfoot: bold totals
    If !LinesMatchHeader: MudAlert Warning "Eilučių suma nesutampa su sąskaitos suma"

  DialogActions: [Atšaukti Text/Default] [Išsaugoti Filled/Primary StartIcon=Save OnClick=SaveAsync]

PHASE "error":
  Centered: big MudIcon Error Style="color:#dc2626;font-size:64px"
  MudAlert Severity.Error: _errorMessage
  MudButton Outlined "Bandyti dar kartą" OnClick="@(() => { _phase=\"upload\"; _file=null; })"
  DialogActions: [Uždaryti Text/Default]

@code:
  [Inject] IExpenseOcrService ExpenseOcrService { get; set; } = default!;
  [Inject] IExpenseService ExpenseService { get; set; } = default!;
  [Inject] ISnackbar Snackbar { get; set; } = default!;
  [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

  private string _phase = "upload";
  private IBrowserFile? _file;
  private int _processingStep = 0;
  private OcrResultDto? _ocrResult;
  private string _errorMessage = "";
  private string _invoiceType = "STANDARD";

  private async Task AnalyzeAsync()
  {
      if (_file == null) return;
      _phase = "processing"; _processingStep = 0; StateHasChanged();
      await Task.Delay(200);
      try
      {
          _processingStep = 1; StateHasChanged();
          var base64 = await ConvertToBase64Async(_file);
          _processingStep = 2; StateHasChanged();
          _ocrResult = await ExpenseOcrService.ProcessAsync(base64, _file.Name);
          _processingStep = 3; StateHasChanged();
          await Task.Delay(400);
          if (!_ocrResult.Success && !string.IsNullOrEmpty(_ocrResult.ErrorMessage))
          { _errorMessage = _ocrResult.ErrorMessage; _phase = "error"; }
          else { _phase = "review"; }
      }
      catch (Exception ex) { _errorMessage = "OCR klaida: " + ex.Message; _phase = "error"; }
      StateHasChanged();
  }

  private async Task SaveAsync()
  {
      if (_ocrResult == null || _file == null) return;
      try
      {
          var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
          var safeName = System.Text.RegularExpressions.Regex.Replace(_file.Name, @"[^a-zA-Z0-9._-]", "_");
          var rel = $"uploads/invoices/{DateTime.Now.Year}/{DateTime.Now.Month:D2}/{timestamp}_{safeName}";
          var full = Path.Combine("wwwroot", rel.Replace("/", Path.DirectorySeparatorChar.ToString()));
          Directory.CreateDirectory(Path.GetDirectoryName(full)!);
          await using var fs = new FileStream(full, FileMode.Create);
          await _file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024).CopyToAsync(fs);
          _ocrResult.OriginalFilePath = rel;
          _ocrResult.OriginalFilename = _file.Name;
          var invoice = await ExpenseService.CreateFromOcrAsync(_ocrResult, "MANUAL");
          Snackbar.Add("Sąskaita išsaugota sėkmingai", Severity.Success);
          MudDialog?.Close(DialogResult.Ok(invoice.Id));
      }
      catch (Exception ex) { Snackbar.Add("Klaida išsaugant: " + ex.Message, Severity.Error); }
  }

  private static async Task<string> ConvertToBase64Async(IBrowserFile file)
  {
      await using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
      using var ms = new MemoryStream();
      await stream.CopyToAsync(ms);
      return Convert.ToBase64String(ms.ToArray());
  }

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite ExpenseUploadDialog as pure UI"
```

---

## PROMPT 7 — InvoiceDetailDialog.razor perrašymas

```
Read .clinerules/nordicbees-standards.md (ALL sections).
Read Services/IExpenseService.cs.
Read Services/IAuthService.cs.
Read Helpers/ExpenseStatusHelper.cs.
Read Models/Expenses/ExpenseInvoice.cs.
Read Components/Dialogs/SupplierCreateDialog.razor (parameters only - first 20 lines of @code).

Rewrite Components/Dialogs/InvoiceDetailDialog.razor completely.
[Parameter] public int InvoiceId { get; set; }
MaxWidth.ExtraLarge, FullWidth=true, CloseButton=true.

CRITICAL: ExpenseInvoiceLines is [NotMapped] AND has entity.Ignore() in DbContext.
Include() will NOT work. ALWAYS load lines separately:
    _lines = await ExpenseService.GetInvoiceLinesAsync(InvoiceId);

TitleContent:
  Receipt icon + "Sąskaita @(_invoice?.InvoiceNumber ?? "...")"
  If !_isLoading: MudIconButton Edit OnClick="@(() => _editMode = !_editMode)"

Layout: MudGrid Spacing=3
  Left column md=5: PDF viewer
  Right column md=7: sections

LEFT COLUMN:
  MudText Typo.overline gray: "Originalus dokumentas"
  If OriginalFilePath not empty:
    <iframe src="/@_invoice.OriginalFilePath"
            style="width:100%;height:620px;border:1px solid #e5e7eb;border-radius:8px" />
  Else:
    Gray MudPaper centered: PictureAsPdf icon + "PDF failas nepridėtas"

RIGHT COLUMN:

Section "Būsena":
  MudChip GetColor/GetLabel from ExpenseStatusHelper
  Flag chips foreach ParseFlags(_invoice.OcrFlags)
  If status=="REJECTED" && RejectedReason not empty:
    Red paper: Cancel icon + _invoice.RejectedReason

Section "Sąskaitos informacija" (blue paper):
  If !_editMode: MudGrid with MudText rows (Nr | Tipas | Data | Terminas | Valiuta)
  If _editMode: MudGrid with MudTextField/MudDatePicker bound to _invoice fields

Section "Sumos" (blue paper):
  If !_editMode: MudGrid (Suma be PVM | PVM% | PVM suma | Suma su PVM bold blue)
  If _editMode: MudGrid with MudNumericField T="decimal"
  Below always: "Sumokėta: {PaidAmount:N2} € | Liko: {AmountInclVat-PaidAmount:N2} €"

Section "Eilutės" (only if _lines.Any()):
  MudText overline "Eilutės"
  MudSimpleTable Dense Striped:
    Th: Aprašymas | Kiekis | Vnt. | Be PVM | PVM% | Su PVM
    Tr foreach _lines
    Tfoot bold: totals
  If AMOUNT_MISMATCH in flags:
    MudAlert Warning "Eilučių suma nesutampa su antraštės suma"

Section "Tiekėjas" (only if status=="PENDING_SUPPLIER", yellow paper):
  SmartToy icon + PendingSupplierName + PendingSupplierVat
  "Sukurti tiekėją" big Filled Primary full width OnClick=CreateSupplierAsync
  Small Text button "Priskirti esamam ▼" OnClick="@(() => _showExistingSearch = !_showExistingSearch)"
  If _showExistingSearch:
    MudAutocomplete T=BusinessPartner @bind-Value=_selectedSupplier
                    SearchFunc=SearchSuppliers ToStringFunc="s => s?.Name ?? \"\""
                    Variant=Outlined Clearable NoItemsFoundText="Nerasta"
    If _selectedSupplier != null:
      MudButton Filled Primary "Patvirtinti" OnClick=ConfirmExistingSupplierAsync

Section "Veiksmai" (only if PENDING, NEEDS_REVIEW, or PARTIAL):
  d-flex gap-2:
    MudButton Filled Primary StartIcon=CheckCircle "Patvirtinti" OnClick=ApproveAsync
    MudButton Outlined Color=Error StartIcon=Cancel "Atmesti"
      OnClick="@(() => _showRejectInput = !_showRejectInput)"
  If _showRejectInput:
    MudTextField @bind-Value=_rejectReason Label="Atmetimo priežastis" Variant=Outlined Lines=2
    MudButton Filled Error "Patvirtinti atmetimą" OnClick=RejectAsync

DialogActions:
  If _editMode:
    MudButton Text Default "Atšaukti" OnClick="@(() => _editMode = false)"
    MudButton Filled Primary StartIcon=Save "Išsaugoti" OnClick=SaveAsync
  Else:
    MudButton Text Default "Uždaryti" OnClick="@(() => MudDialog?.Cancel())"

@code:
  [Inject] IExpenseService ExpenseService { get; set; } = default!;
  [Inject] IAuthService AuthService { get; set; } = default!;
  [Inject] IDialogService DialogService { get; set; } = default!;
  [Inject] IDbContextFactory<NordicBeesERPContext> DbFactory { get; set; } = default!;
  [Inject] ISnackbar Snackbar { get; set; } = default!;
  [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;
  [Parameter] public int InvoiceId { get; set; }

  private ExpenseInvoice? _invoice;
  private List<ExpenseInvoiceLine> _lines = new();
  private List<BusinessPartner> _suppliers = new();
  private BusinessPartner? _selectedSupplier;
  private bool _isLoading = true;
  private bool _editMode = false;
  private bool _showExistingSearch = false;
  private bool _showRejectInput = false;
  private string _rejectReason = "";
  private int _loadedInvoiceId = 0;

  protected override async Task OnParametersSetAsync()
  {
      if (_loadedInvoiceId == InvoiceId) return;
      _loadedInvoiceId = InvoiceId;
      _isLoading = true;
      _invoice = await ExpenseService.GetInvoiceWithDetailsAsync(InvoiceId);
      // CRITICAL: load lines separately - Include() does not work due to [NotMapped]
      _lines = await ExpenseService.GetInvoiceLinesAsync(InvoiceId);
      if (_invoice?.Status == "PENDING_SUPPLIER")
      {
          await using var ctx = DbFactory.CreateDbContext();
          _suppliers = await ctx.BusinessPartners
              .Where(b => (b.PartnerType == PartnerType.ExpenseSupplier || b.PartnerType == PartnerType.Both) && b.IsActive)
              .OrderBy(b => b.Name).ToListAsync();
      }
      _isLoading = false;
  }

  private Task<IEnumerable<BusinessPartner>> SearchSuppliers(string val, CancellationToken ct)
  {
      if (string.IsNullOrEmpty(val)) return Task.FromResult(_suppliers.AsEnumerable());
      return Task.FromResult(_suppliers.Where(s =>
          s.Name.Contains(val, StringComparison.OrdinalIgnoreCase) ||
          (!string.IsNullOrEmpty(s.VatCode) && s.VatCode.Contains(val))));
  }

  private async Task ApproveAsync()
  {
      var user = await AuthService.GetAuthenticatedUserAsync();
      await ExpenseService.ApproveAsync(InvoiceId, user?.FullName ?? "system");
      Snackbar.Add("Sąskaita patvirtinta sėkmingai", Severity.Success);
      MudDialog?.Close(DialogResult.Ok(true));
  }

  private async Task RejectAsync()
  {
      if (string.IsNullOrEmpty(_rejectReason))
      { Snackbar.Add("Įveskite atmetimo priežastį", Severity.Warning); return; }
      var user = await AuthService.GetAuthenticatedUserAsync();
      await ExpenseService.RejectAsync(InvoiceId, _rejectReason, user?.FullName ?? "system");
      Snackbar.Add("Sąskaita atmesta", Severity.Info);
      MudDialog?.Close(DialogResult.Ok(true));
  }

  private async Task CreateSupplierAsync()
  {
      var p = new DialogParameters();
      p.Add("PrefilledName", _invoice?.PendingSupplierName ?? "");
      p.Add("PrefilledVatCode", _invoice?.PendingSupplierVat ?? "");
      p.Add("PrefilledAddress", _invoice?.PendingSupplierAddress ?? "");
      p.Add("PrefilledCity", _invoice?.PendingSupplierCity ?? "");
      p.Add("PrefilledPostalCode", _invoice?.PendingSupplierPostalCode ?? "");
      p.Add("PrefilledCountryCode", _invoice?.PendingSupplierCountryCode ?? "LT");
      p.Add("PrefilledCompanyCode", _invoice?.PendingSupplierCompanyCode ?? "");
      p.Add("PrefilledBankAccount", _invoice?.PendingSupplierBankAccount ?? "");
      var dlg = await DialogService.ShowAsync<SupplierCreateDialog>("Sukurti tiekėją", p,
          new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
      var result = await dlg.Result;
      if (result is { Canceled: false })
      {
          await using var ctx = DbFactory.CreateDbContext();
          var vatCode = _invoice?.PendingSupplierVat;
          var name = _invoice?.PendingSupplierName;
          BusinessPartner? newSupplier = null;
          if (!string.IsNullOrEmpty(vatCode))
              newSupplier = await ctx.BusinessPartners.FirstOrDefaultAsync(b => b.VatCode == vatCode);
          newSupplier ??= await ctx.BusinessPartners
              .Where(b => b.Name.Contains(name ?? ""))
              .OrderByDescending(b => b.Id).FirstOrDefaultAsync();
          if (newSupplier != null)
          {
              var user = await AuthService.GetAuthenticatedUserAsync();
              await ExpenseService.AssignSupplierAsync(InvoiceId, newSupplier.Id, user?.FullName ?? "system");
              Snackbar.Add("Tiekėjas sukurtas ir priskirtas", Severity.Success);
              MudDialog?.Close(DialogResult.Ok(true));
          }
          else Snackbar.Add("Tiekėjas sukurtas, bet nepavyko automatiškai priskirti — priskirkite rankiniu būdu", Severity.Warning);
      }
  }

  private async Task ConfirmExistingSupplierAsync()
  {
      if (_selectedSupplier == null) return;
      var user = await AuthService.GetAuthenticatedUserAsync();
      await ExpenseService.AssignSupplierAsync(InvoiceId, _selectedSupplier.Id, user?.FullName ?? "system");
      Snackbar.Add("Tiekėjas priskirtas", Severity.Success);
      MudDialog?.Close(DialogResult.Ok(true));
  }

  private async Task SaveAsync()
  {
      try
      {
          if (_invoice == null) return;
          await ExpenseService.UpdateInvoiceAsync(_invoice);
          Snackbar.Add("Išsaugota sėkmingai", Severity.Success);
          _editMode = false;
          MudDialog?.Close(DialogResult.Ok(true));
      }
      catch (Exception ex) { Snackbar.Add("Klaida: " + ex.Message, Severity.Error); }
  }

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite InvoiceDetailDialog - PDF viewer, lines (loaded separately), approve/reject"
```

---

## PROMPT 8 — AssignSupplierDialog.razor perrašymas

```
Read .clinerules/nordicbees-standards.md (ALL sections).
Read Services/IExpenseService.cs.
Read Services/IAuthService.cs.

Rewrite Components/Dialogs/AssignSupplierDialog.razor completely.
[Parameter] public int InvoiceId { get; set; }

OnParametersSetAsync:
  Guard: if (_loadedInvoiceId == InvoiceId) return;
  Load invoice via DbFactory.FindAsync(InvoiceId)
  Load suppliers: BusinessPartners where PartnerType in (ExpenseSupplier, Both) and IsActive=true

Layout:
  TitleContent: AssignmentInd icon + "Priskirti tiekėją"
  Invoice info (blue paper): Nr, Data, Suma
  OCR detected (yellow paper, only if PendingSupplierName not empty): name + VAT
  Big Filled Primary button full-width height=52px "Sukurti tiekėją" → CreateSupplierAsync()
  Small Text button "Priskirti esamam ▼" toggle _showExistingSearch
  If _showExistingSearch: MudAutocomplete + Patvirtinti button

After SupplierCreateDialog result (!Canceled):
  Reload suppliers from DB
  Find by VAT, then by name, then OrderByDescending(Id).First
  If found: ExpenseService.AssignSupplierAsync(InvoiceId, id, performedBy)
  MudDialog?.Close(DialogResult.Ok(true))

All performedBy from IAuthService.GetAuthenticatedUserAsync()?.FullName ?? "system"

CancelAsync: if status==PENDING_SUPPLIER show ShowMessageBox confirmation

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite AssignSupplierDialog"
```

---

## PROMPT 9 — ExpenseInvoices.razor perrašymas

```
Read .clinerules/nordicbees-standards.md (ALL sections).
Read Services/IExpenseService.cs.
Read Helpers/ExpenseStatusHelper.cs.

Rewrite Components/Pages/ExpenseInvoices.razor completely.

Header: MudText h5 "Išlaidų sąskaitos" + right: [Įkelti sąskaitą Filled/Primary] [Eksportuoti Outlined]

If _attentionInvoices.Any():
  MudAlert Warning: "@_attentionInvoices.Count sąskaita(-os) reikalauja dėmesio"

Filters (per standards section 13):
  Instant search + DateRangePicker + clear button

MudTabs ActivePanelIndexChanged:
  "VISOS" "NESUMOKĖTOS" "VĖLUOJANČIOS" "ŠIS MĖNUO" "ULAK" "REIKIA DĖMESIO"

MudTable Dense Hover Striped RowsPerPage=25 PagerContent:
  Columns: Nr | Tiekėjas | Data | Terminas | Suma | Liko | Statusas | Problemos | 👁
  OnRowClick: open InvoiceDetailDialog(InvoiceId), on close reload
  Terminas colored: red if overdue, orange if <=7d, green otherwise
  Problemos: flag chips via ParseFlags/GetFlagColor/GetFlagLabel

Year toggle switch.

@code LoadDataAsync():
  GetInvoicesAsync with year filter
  Load supplier names for all SupplierId values
  _attentionInvoices = NeedsAttention filter

FilteredInvoices:
  "all" → InvoiceType != "ULAK"
  "unpaid" → PENDING or PARTIAL
  "overdue" → DueDate < today, not paid
  "thismonth" → this month
  "ulak" → InvoiceType=="ULAK"
  "attention" → NeedsAttention

GetSupplierDisplay: SupplierId→name from dict, else PendingSupplierName ?? "Nežinomas"

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite ExpenseInvoices with tabs, filters, flags"
```

---

## PROMPT 10 — ExpensesDashboard.razor perrašymas

```
Read .clinerules/nordicbees-standards.md (ALL sections, section 12 KPI KORTELĖS).
Read Services/IExpenseService.cs.
Read Helpers/ExpenseStatusHelper.cs.

Rewrite Components/Pages/ExpensesDashboard.razor completely.
Model exactly after PaymentsDashboard.razor KPI card style.

4 KPI cards:
  Blue  #dbeafe: "Šių metų išlaidos" total (excl ULAK)
  Red   #fef2f2: "Nesumokėta" PENDING+PARTIAL remaining
  Yellow #fef3c7: "Vėluoja" overdue not paid
  Purple #f5f3ff: "Iki 7 dienų" due in 0-7 days

Each card: caption, yearSubtitle, h4 amount, caption PVM, caption total with PVM, caption count.

Attention alert if _attentionCount > 0:
  Grouped by reason with counts
  Link "Peržiūrėti visas →" to /expense-invoices

Year toggle switch.

Recent invoices table (last 10):
  Compact MudTable Dense Hover Striped RowsPerPage=10
  Same columns as ExpenseInvoices
  "Rodyti visas →" link

LoadDataAsync: GetInvoicesAsync filtered by year, exclude ULAK from stats.

Run: dotnet build 2>&1 | grep "error CS"
Commit: "refactor: rewrite ExpensesDashboard KPI style"
```

---

## PROMPT 11 — expense_categories atnaujinimas

```
Read .clinerules/nordicbees-standards.md section 4.

Using MySQL MCP:
DELETE FROM expense_categories;
INSERT INTO expense_categories (name, code, is_active, sort_order) VALUES
  ('Žaliavos', 'ZALIAVOS', 1, 1),
  ('Pakavimo medžiagos', 'PAKAVIMAS', 1, 2),
  ('Elektra', 'ELEKTRA', 1, 3),
  ('Kuras', 'KURAS', 1, 4),
  ('Automobilių remontas', 'AUTO_REMONTAS', 1, 5),
  ('Gamybos dalys ir įrankiai', 'GAMYBOS_DALYS', 1, 6),
  ('Transporto paslaugos', 'TRANSPORTAS', 1, 7),
  ('Ryšių ir IT paslaugos', 'RYSIAI_IT', 1, 8),
  ('Administravimas', 'ADMINISTRACIJA', 1, 9),
  ('Draudimas', 'DRAUDIMAS', 1, 10),
  ('Kita', 'KITA', 1, 99);

Verify: SELECT * FROM expense_categories ORDER BY sort_order;

Read Components/Pages/ExpenseSettings.razor (current structure only).
Add categories management section:
  MudTable Dense Striped of categories
  Columns: Pavadinimas | Kodas | Aktyvus toggle | Eilės nr | Veiksmai (edit/delete)
  [+ Pridėti] button → inline new row or small dialog
  Delete: ShowMessageBox confirmation, then set is_active=false (soft delete)

Run: dotnet build 2>&1 | grep "error CS"
Commit: "feat: Lakstena categories, categories management UI"
```

---

## PROMPT 12 — Galutinis patikrinimas

```
Read .clinerules/nordicbees-standards.md section 23 (DRAUDŽIAMA).

1. dotnet build 2>&1 — must be 0 errors

2. grep -rn "Console.WriteLine" Services/ExpenseOcrService.cs Services/ExpenseService.cs Components/Dialogs/ExpenseUploadDialog.razor Components/Dialogs/InvoiceDetailDialog.razor

3. grep -n "LT100013406816\|LT254724219" Services/ExpenseOcrService.cs
   (must be empty - no hardcoded VAT codes)

4. grep -n "bank_transfer" Services/ExpenseService.cs Components/Dialogs/
   (must be empty in expense module)

5. grep -n "ExtractInvoiceDataAsync" Services/OcrQueueWorker.cs Services/IExpenseOcrService.cs Services/ExpenseOcrService.cs
   (must exist in all three - OcrQueueWorker still uses it)

6. dotnet build 2>&1 | grep "error CS"

7. git add -A && git commit -m "feat: expense module full rewrite complete" && git push
```

---

## Svarbios pastabos

1. **Eilės tvarka privaloma** — kiekvienas prompt'as priklauso nuo ankstesnio
2. **0 build errors** po kiekvieno prompt'o prieš einant toliau
3. **`ExtractInvoiceDataAsync`** — PRIVALOMA palikti kaip aliasą `ProcessAsync`
4. **`GetInvoiceLinesAsync`** — visada naudoti eilutėms, ne `Include()`
5. **`project_id`** — neimplementuojame, paliekame ateičiai
6. **`AssignSupplierDialog`** — reikalingas iš `ExpenseInvoices` sąrašo
7. **`performedBy`** — visada iš `IAuthService`, ne hardcoded
