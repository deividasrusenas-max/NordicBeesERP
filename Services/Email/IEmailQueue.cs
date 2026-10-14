namespace NordicBeesERP.Services.Email;

public sealed class EmailAttachmentInput
{
    public byte[] Content { get; init; } = Array.Empty<byte>();
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
}

public sealed class EmailRequest
{
    public IReadOnlyList<string> To { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string>? Cc { get; init; }
    public string? ReplyTo { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
    public string? BodyText { get; init; }
    public IReadOnlyList<EmailAttachmentInput>? Attachments { get; init; }
    public string? RelatedType { get; init; }
    public int? RelatedId { get; init; }
    public int? CreatedBy { get; init; }
}

public interface IEmailQueue
{
    /// <summary>
    /// Validates the request, stores attachments via IFileStore and inserts the outbox rows ('queued').
    /// Returns the new email_outbox.id. Throws ArgumentException for invalid input (empty/unparseable recipients, etc.).
    /// </summary>
    Task<int> EnqueueAsync(EmailRequest req, CancellationToken ct = default);
}
