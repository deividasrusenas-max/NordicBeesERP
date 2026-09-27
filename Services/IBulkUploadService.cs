namespace NordicBeesERP.Services;

/// <summary>
/// D-048 item 1 (Part B): runs one PDF through the exact same pipeline the upload dialog uses
/// (<see cref="IExpenseOcrService"/>, <see cref="Storage.IFileStore"/>, <see cref="IExpenseService.CreateFromOcrAsync"/>)
/// with no per-document preview — the rules alone decide the invoice's status, exactly as they
/// would for the dialog's own review-and-save flow. Nothing is auto-approved and no supplier is
/// auto-created; a document that would need a human's attention in the dialog still needs it
/// afterward here, through its status (NEEDS_REVIEW, PENDING_SUPPLIER, DUPLICATE_PENDING, …).
/// </summary>
public interface IBulkUploadService
{
    /// <summary>Processes exactly one file. Never throws for an expected refusal (not a digital
    /// PDF, a duplicate, an OCR failure) — those come back as a refused <see cref="BulkUploadFileResult"/>
    /// so the caller can keep going with the next file.</summary>
    Task<BulkUploadFileResult> ProcessFileAsync(byte[] pdfBytes, string fileName, Guid batchId, CancellationToken ct = default);
}

public sealed class BulkUploadFileResult
{
    public required string FileName { get; init; }
    public bool Accepted { get; init; }

    /// <summary>Set only when Accepted is false — the same Lithuanian message the dialog would
    /// show for the same refusal (not a digital PDF, duplicate file, OCR failure).</summary>
    public string? RefusalReason { get; init; }

    public int? InvoiceId { get; init; }
    public string? Status { get; init; }
    public IReadOnlyList<string> Flags { get; init; } = Array.Empty<string>();

    /// <summary>"Priskirtas: <name>" when the matcher found a confirmed supplier, otherwise a
    /// short Lithuanian note ("Nežinomas tiekėjas", …) — for the progress list, not a decision.</summary>
    public string? SupplierOutcome { get; init; }
}
