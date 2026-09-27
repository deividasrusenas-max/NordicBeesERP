using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services.Storage;

namespace NordicBeesERP.Services;

/// <inheritdoc cref="IBulkUploadService"/>
public sealed class BulkUploadService : IBulkUploadService
{
    /// <summary>Same limit as the upload dialog's file picker/drop zone (<c>ExpenseUploadDialog.razor</c>,
    /// <c>MaxUploadBytes</c>) — kept identical rather than shared, since the dialog's field is
    /// private and not a frozen member worth exposing just for this constant.</summary>
    public const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IExpenseOcrService _ocrService;
    private readonly IExpenseService _expenseService;
    private readonly IFileStore _fileStore;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly ILogger<BulkUploadService> _logger;

    public BulkUploadService(
        IExpenseOcrService ocrService,
        IExpenseService expenseService,
        IFileStore fileStore,
        IDbContextFactory<NordicBeesERPContext> dbFactory,
        ILogger<BulkUploadService> logger)
    {
        _ocrService = ocrService;
        _expenseService = expenseService;
        _fileStore = fileStore;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<BulkUploadFileResult> ProcessFileAsync(byte[] pdfBytes, string fileName, Guid batchId, CancellationToken ct = default)
    {
        // Same order as ExpenseUploadDialog.razor's AnalyzeAsync: extension, then signature +
        // text-layer (D-032), then the SHA-256 duplicate check, all before any Azure call.
        if (!PdfIntakeCheck.HasPdfExtension(fileName))
        {
            return Refused(fileName, PdfIntakeCheck.Message(PdfIntakeResult.NotPdf)!);
        }

        if (pdfBytes.LongLength > MaxUploadBytes)
        {
            return Refused(fileName, "Failas per didelis (daugiausia 10 MB).");
        }

        var intake = PdfIntakeCheck.Check(pdfBytes);
        if (intake != PdfIntakeResult.Ok)
        {
            return Refused(fileName, PdfIntakeCheck.Message(intake)!);
        }

        var sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdfBytes)).ToLowerInvariant();
        foreach (var linkedId in await _fileStore.FindLinkedEntityIdsAsync(sha256, "expenses", ct))
        {
            var existing = await _expenseService.GetInvoiceAsync((int)linkedId);
            if (existing == null) continue;
            return Refused(fileName, $"Šis failas jau įkeltas: sąskaita Nr. {existing.InvoiceNumber} ({existing.Status}).");
        }

        var base64 = Convert.ToBase64String(pdfBytes);
        var result = await _ocrService.ProcessAsync(base64, fileName);

        var isOcrFailed = string.IsNullOrEmpty(result.InvoiceNumber) &&
                           result.AmountInclVat == 0 &&
                           string.IsNullOrEmpty(result.SupplierName);
        if (isOcrFailed && !string.IsNullOrEmpty(result.Diagnostics.AzureError))
        {
            return Refused(fileName, $"OCR nepavyko. Serverių būsena: {result.Diagnostics.AzureError}");
        }

        // CreateFromOcrAsync (via EnsureInvoiceNumberPresent) throws if the invoice number is
        // blank, regardless of whether amount/supplier were extracted fine — a real, ordinary
        // partial-OCR outcome. That must be caught HERE, before FileStore.SaveAsync, not after:
        // SaveAsync already persists a files row + blob, and FindLinkedEntityIdsAsync only matches
        // rows with entity_id set, so a row saved just before an exception is an orphan that no
        // later duplicate check will ever catch, on every re-upload of the same file.
        if (string.IsNullOrWhiteSpace(result.InvoiceNumber))
        {
            return Refused(fileName, "Sąskaitos numeris neatpažintas. Reikalinga rankinė peržiūra.");
        }

        var safeFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Path.GetFileNameWithoutExtension(fileName)}.pdf";
        var storedFile = await _fileStore.SaveAsync(
            new MemoryStream(pdfBytes),
            new FileMetadata("expenses", "expense_invoice", null, "application/pdf", safeFileName),
            ct);

        result.FileId = storedFile.Id;
        result.OriginalFilename = safeFileName;

        // The Source column is a MySQL ENUM('MANUAL','EMAIL','N8N') — 'BULK' is not a valid value
        // and no DDL may add one this session, so this stays "MANUAL" (a bulk upload is still an
        // administrator-driven upload, not the automated EMAIL/N8N path). The batch id and the
        // fact that this specific invoice came from a bulk run are recorded separately below, in
        // a second audit row — this keeps CreateFromOcrAsync's own "CREATED" audit entry meaning
        // exactly what it always has for every other caller (D-048's own instruction).
        var invoice = await _expenseService.CreateFromOcrAsync(result, "MANUAL");

        await using (var ctx = await _dbFactory.CreateDbContextAsync(ct))
        {
            ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Action = "BULK_CREATED",
                ActionDetails = $"Partijos ID: {batchId}",
                OldStatus = null,
                NewStatus = invoice.Status,
                PerformedBy = "BULK_UPLOAD",
                PerformedAt = DateTime.Now
            });
            await ctx.SaveChangesAsync(ct);
        }

        var supplierOutcome = invoice.SupplierId.HasValue
            ? $"Priskirtas tiekėjas (ID {invoice.SupplierId})"
            : invoice.Status == "PENDING_SUPPLIER"
                ? "Nežinomas tiekėjas"
                : "—";

        return new BulkUploadFileResult
        {
            FileName = fileName,
            Accepted = true,
            InvoiceId = invoice.Id,
            Status = invoice.Status,
            Flags = result.Flags,
            SupplierOutcome = supplierOutcome
        };
    }

    private BulkUploadFileResult Refused(string fileName, string reason)
    {
        _logger.LogInformation("Bulk upload refused {FileName}: {Reason}", fileName, reason);
        return new BulkUploadFileResult { FileName = fileName, Accepted = false, RefusalReason = reason };
    }
}
