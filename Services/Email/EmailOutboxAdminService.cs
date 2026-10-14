using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Storage;

namespace NordicBeesERP.Services.Email;

public sealed record OutboxListRow(
    int Id, string To, string? OriginalTo, string Subject, string Status, int Attempts, string? RelatedType, int? RelatedId,
    string? LastError, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime NextAttemptAtUtc);

public sealed record OutboxAttachmentRow(int Id, string FileName, string ContentType, long FileId, int SizeBytes);

public sealed record OutboxDetail(OutboxListRow Row, string? Cc, string? ReplyTo, string BodyHtml, string? BodyText, IReadOnlyList<OutboxAttachmentRow> Attachments);

public sealed record EmailModeInfo(EmailMode Mode, string? RedirectTo, string EnvironmentName);

/// <summary>Read/maintenance operations on email_outbox for the admin UI. Sending itself stays in EmailOutboxWorker (P1).</summary>
public class EmailOutboxAdminService
{
    private const string ListColumns =
        "id, to_addresses, original_to, subject, status, attempts, related_type, related_id, last_error, created_at, sent_at, next_attempt_at";

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IFileStore _fileStore;
    private readonly IEmailQueue _queue;
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _env;

    public EmailOutboxAdminService(
        IDbContextFactory<NordicBeesERPContext> dbFactory, IFileStore fileStore, IEmailQueue queue,
        IOptions<EmailOptions> options, IHostEnvironment env)
    {
        _dbFactory = dbFactory;
        _fileStore = fileStore;
        _queue = queue;
        _options = options.Value;
        _env = env;
    }

    public EmailModeInfo GetModeInfo() =>
        new(EmailSafetyGuard.ResolveEffectiveMode(_env.EnvironmentName, _options),
            string.IsNullOrWhiteSpace(_options.RedirectTo) ? null : _options.RedirectTo.Trim(), _env.EnvironmentName);

    private static OutboxListRow MapRow(System.Data.Common.DbDataReader r) => new(
        r.Int("id"), r.Str("to_addresses") ?? string.Empty, r.Str("original_to"), r.Str("subject") ?? string.Empty,
        r.Str("status") ?? string.Empty, r.Int("attempts"), r.Str("related_type"), r.IntOrNull("related_id"), r.Str("last_error"),
        r.Utc("created_at"), r.UtcOrNull("sent_at"), r.Utc("next_attempt_at"));

    public async Task<(int Total, List<OutboxListRow> Rows)> PageAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var total = Convert.ToInt32(await RawSql.ScalarAsync(db,
            "SELECT COUNT(*) FROM email_outbox WHERE (@p0 IS NULL OR status = @p0)", ct, status) ?? 0);
        var rows = await RawSql.QueryAsync(db,
            $"SELECT {ListColumns} FROM email_outbox WHERE (@p0 IS NULL OR status = @p0) ORDER BY id DESC LIMIT @p1 OFFSET @p2",
            MapRow, ct, status, pageSize, Math.Max(0, page) * pageSize);
        return (total, rows);
    }

    public async Task<List<OutboxListRow>> ListForRelatedAsync(string relatedType, int relatedId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await RawSql.QueryAsync(db,
            $"SELECT {ListColumns} FROM email_outbox WHERE related_type = @p0 AND related_id = @p1 ORDER BY id DESC LIMIT 50",
            MapRow, ct, relatedType, relatedId);
    }

    public async Task<OutboxDetail?> GetDetailAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await RawSql.QueryAsync(db,
            $"SELECT {ListColumns}, cc_addresses, reply_to, body_html, body_text FROM email_outbox WHERE id = @p0",
            r => (Row: MapRow(r), Cc: r.Str("cc_addresses"), ReplyTo: r.Str("reply_to"), Html: r.Str("body_html") ?? string.Empty, Text: r.Str("body_text")),
            ct, id);
        if (rows.Count == 0) return null;
        var attachments = await RawSql.QueryAsync(db,
            "SELECT id, file_name, content_type, file_id, size_bytes FROM email_outbox_attachments WHERE outbox_id = @p0 ORDER BY id",
            r => new OutboxAttachmentRow(r.Int("id"), r.Str("file_name") ?? string.Empty, r.Str("content_type") ?? string.Empty, r.Long("file_id"), r.Int("size_bytes")),
            ct, id);
        var x = rows[0];
        return new OutboxDetail(x.Row, x.Cc, x.ReplyTo, x.Html, x.Text, attachments);
    }

    /// <summary>queued → cancelled. False when the row is no longer queued (e.g. the worker just claimed it).</summary>
    public async Task<bool> CancelAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var n = await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_outbox SET status = 'cancelled', updated_at = {1} WHERE id = {0} AND status = 'queued'", id, DateTime.UtcNow);
        return n == 1;
    }

    /// <summary>failed → queued with attempts reset and next_attempt_at now.</summary>
    public async Task<bool> RetryAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var n = await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_outbox SET status = 'queued', attempts = 0, next_attempt_at = {1}, last_error = NULL, updated_at = {1} WHERE id = {0} AND status = 'failed'",
            id, now);
        return n == 1;
    }

    public async Task<(string FileName, string ContentType, byte[] Bytes)?> DownloadAttachmentAsync(int attachmentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await RawSql.QueryAsync(db, "SELECT file_name, content_type, file_id FROM email_outbox_attachments WHERE id = @p0",
            r => (Name: r.Str("file_name") ?? "attachment", Type: r.Str("content_type") ?? "application/octet-stream", FileId: r.Long("file_id")), ct, attachmentId);
        if (rows.Count == 0) return null;
        await using var stream = await _fileStore.OpenAsync(rows[0].FileId, ct);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return (rows[0].Name, rows[0].Type, ms.ToArray());
    }

    /// <summary>Queues a test message to RedirectTo (or the given fallback address). The safety guard still applies when it is sent.</summary>
    public async Task<int> EnqueueTestAsync(string? fallbackAddress, CancellationToken ct = default)
    {
        var to = !string.IsNullOrWhiteSpace(_options.RedirectTo) ? _options.RedirectTo : fallbackAddress;
        if (string.IsNullOrWhiteSpace(to)) throw new InvalidOperationException("No recipient address for the test message.");
        var stamp = VilniusTimeText.Now();
        return await _queue.EnqueueAsync(new EmailRequest
        {
            To = EmailOutboxAdminService.SplitList(to),
            Subject = "Testinis laiškas – NordicBeesERP",
            BodyHtml = $"<p>Tai testinis laiškas iš NordicBeesERP ({System.Net.WebUtility.HtmlEncode(_env.EnvironmentName)}).</p><p>Išsiuntimo laikas: {stamp}.</p>",
            RelatedType = "test"
        }, ct);
    }

    public static List<string> SplitList(string raw) =>
        raw.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

internal static class VilniusTimeText
{
    public static string Now() => Dunning.VilniusTime.ToLocal(DateTime.UtcNow).ToString("yyyy-MM-dd HH:mm");
}
