using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MimeKit;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Storage;

namespace NordicBeesERP.Services.Email;

public class EmailQueue : IEmailQueue
{
    public const string FileModule = "email";
    public const string FileEntityType = "outbox_attachment";

    private const int MaxAddressListLength = 1000;
    private const int MaxReplyToLength = 255;
    private const int MaxSubjectLength = 500;
    private const int MaxFileNameLength = 255;
    private const int MaxContentTypeLength = 100;
    private const int MaxRelatedTypeLength = 50;

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IFileStore _fileStore;

    public EmailQueue(IDbContextFactory<NordicBeesERPContext> dbFactory, IFileStore fileStore)
    {
        _dbFactory = dbFactory;
        _fileStore = fileStore;
    }

    public async Task<int> EnqueueAsync(EmailRequest req, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(req);

        var to = NormalizeAddressList(req.To, "To", required: true)!;
        var cc = NormalizeAddressList(req.Cc, "Cc", required: false);
        var replyTo = NormalizeReplyTo(req.ReplyTo);

        if (string.IsNullOrWhiteSpace(req.Subject))
            throw new ArgumentException("Subject is required.", nameof(req));
        if (req.Subject.Length > MaxSubjectLength)
            throw new ArgumentException($"Subject is longer than {MaxSubjectLength} characters.", nameof(req));
        if (string.IsNullOrWhiteSpace(req.BodyHtml))
            throw new ArgumentException("BodyHtml is required.", nameof(req));
        if (req.RelatedType is { Length: > MaxRelatedTypeLength })
            throw new ArgumentException($"RelatedType is longer than {MaxRelatedTypeLength} characters.", nameof(req));

        var attachments = req.Attachments ?? Array.Empty<EmailAttachmentInput>();
        foreach (var a in attachments)
        {
            if (a.Content.Length == 0)
                throw new ArgumentException($"Attachment '{a.FileName}' is empty.", nameof(req));
            if (string.IsNullOrWhiteSpace(a.FileName) || a.FileName.Length > MaxFileNameLength)
                throw new ArgumentException("Attachment file name is missing or too long.", nameof(req));
            if (string.IsNullOrWhiteSpace(a.ContentType) || a.ContentType.Length > MaxContentTypeLength)
                throw new ArgumentException("Attachment content type is missing or too long.", nameof(req));
        }

        // Blobs first: content-addressed + deduplicated, so an orphan left by a failed insert is harmless.
        var storedAttachments = new List<(EmailAttachmentInput Input, StoredFile File)>();
        foreach (var a in attachments)
        {
            await using var content = new MemoryStream(a.Content, writable: false);
            var stored = await _fileStore.SaveAsync(
                content,
                new FileMetadata(FileModule, FileEntityType, null, a.ContentType, a.FileName, req.CreatedBy?.ToString()),
                ct);
            storedAttachments.Add((a, stored));
        }

        var now = DateTime.UtcNow;
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var conn = db.Database.GetDbConnection();
        // Keep one connection open so LAST_INSERT_ID() belongs to our INSERT.
        await conn.OpenAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO email_outbox (to_addresses, cc_addresses, reply_to, subject, body_html, body_text, status, attempts, next_attempt_at, related_type, related_id, created_by, created_at, updated_at) " +
                "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, 'queued', 0, {6}, {7}, {8}, {9}, {10}, {11})",
                new object?[]
                {
                    to, cc, replyTo, req.Subject, req.BodyHtml, req.BodyText,
                    now, req.RelatedType, req.RelatedId, req.CreatedBy, now, now
                });

            int outboxId;
            using (var idCmd = conn.CreateCommand())
            {
                idCmd.Transaction = tx.GetDbTransaction();
                idCmd.CommandText = "SELECT LAST_INSERT_ID()";
                outboxId = Convert.ToInt32(await idCmd.ExecuteScalarAsync(ct));
            }

            foreach (var (input, file) in storedAttachments)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO email_outbox_attachments (outbox_id, file_name, content_type, file_id, size_bytes) VALUES ({0}, {1}, {2}, {3}, {4})",
                    new object?[] { outboxId, input.FileName, input.ContentType, file.Id, input.Content.Length });
            }

            await tx.CommitAsync(ct);
            return outboxId;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    /// <summary>
    /// Trims, parses (MimeKit MailboxAddress.TryParse) and joins addresses with ", ". Throws ArgumentException
    /// when a required list is empty, an address is unparseable, or the joined text exceeds the column width.
    /// Returns null for an optional list with no entries.
    /// </summary>
    public static string? NormalizeAddressList(IEnumerable<string>? addresses, string field, bool required)
    {
        var parsed = new List<string>();
        foreach (var raw in addresses ?? Enumerable.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (!TryParseRemote(raw, out var mailbox))
                throw new ArgumentException($"Invalid e-mail address in {field}.", field);
            parsed.Add(mailbox.Address);
        }

        if (parsed.Count == 0)
        {
            return required ? throw new ArgumentException($"{field} must contain at least one recipient.", field) : null;
        }

        var joined = string.Join(", ", parsed);
        if (joined.Length > MaxAddressListLength)
            throw new ArgumentException($"{field} is longer than {MaxAddressListLength} characters.", field);
        return joined;
    }

    // MimeKit accepts local-only mailboxes ("john"); outbound mail needs local@domain.
    private static bool TryParseRemote(string raw, out MailboxAddress mailbox)
    {
        if (MailboxAddress.TryParse(raw.Trim(), out var parsed) && parsed is not null)
        {
            var at = parsed.Address.IndexOf('@');
            if (at > 0 && at < parsed.Address.Length - 1)
            {
                mailbox = parsed;
                return true;
            }
        }

        mailbox = null!;
        return false;
    }

    private static string? NormalizeReplyTo(string? replyTo)
    {
        if (string.IsNullOrWhiteSpace(replyTo))
            return null;
        if (!TryParseRemote(replyTo, out var mailbox))
            throw new ArgumentException("Invalid e-mail address in ReplyTo.", nameof(replyTo));
        if (mailbox.Address.Length > MaxReplyToLength)
            throw new ArgumentException($"ReplyTo is longer than {MaxReplyToLength} characters.", nameof(replyTo));
        return mailbox.Address;
    }
}
