namespace NordicBeesERP.Models.Email;

public enum EmailOutboxStatus
{
    Queued,
    Sending,
    Sent,
    Failed,
    Cancelled
}

/// <summary>
/// Row of <c>email_outbox</c>. Read/written only via raw SQL (global NoTracking, no DbSet).
/// Nullable properties mirror the NULL columns; all timestamps are UTC.
/// </summary>
public class EmailOutbox
{
    public int Id { get; set; }
    public string ToAddresses { get; set; } = string.Empty;
    public string? CcAddresses { get; set; }
    public string? ReplyTo { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public EmailOutboxStatus Status { get; set; } = EmailOutboxStatus.Queued;
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public string? MessageId { get; set; }
    public DateTime? SentAt { get; set; }
    public string? OriginalTo { get; set; }
    public string? RelatedType { get; set; }
    public int? RelatedId { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Row of <c>email_outbox_attachments</c>; <see cref="FileId"/> references <c>files.id</c> (IFileStore).</summary>
public class EmailOutboxAttachment
{
    public int Id { get; set; }
    public int OutboxId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileId { get; set; }
    public int SizeBytes { get; set; }
}
