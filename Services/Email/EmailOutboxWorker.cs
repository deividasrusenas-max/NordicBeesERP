using System.Data.Common;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Email;
using NordicBeesERP.Services.Storage;

namespace NordicBeesERP.Services.Email;

/// <summary>
/// Drains email_outbox. Polls every 30 s (same shape as ArtworkPreviewWorker), claims rows atomically with
/// UPDATE ... WHERE status='queued', and sends via MailKit. Never connects to SMTP while the effective mode is Disabled
/// or the SMTP settings are incomplete (staging has no password on purpose). Never logs passwords or message bodies.
/// </summary>
public class EmailOutboxWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StuckSendingAfter = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan NotSendingLogInterval = TimeSpan.FromMinutes(1);
    private const int BatchSize = 10;
    private const int LastErrorMaxLength = 2000;
    private const int NetworkTimeoutMs = 60_000;

    private readonly ILogger<EmailOutboxWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IHostEnvironment _env;
    private readonly EmailOptions _options;
    private DateTime _lastNotSendingLogUtc = DateTime.MinValue;

    public EmailOutboxWorker(
        ILogger<EmailOutboxWorker> logger,
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<NordicBeesERPContext> dbFactory,
        IHostEnvironment env,
        IOptions<EmailOptions> options)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _dbFactory = dbFactory;
        _env = env;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email Outbox Worker starting at {Time} (environment {Env}, configured mode {Mode}, effective mode {Effective})",
            DateTimeOffset.Now, _env.EnvironmentName, _options.Mode, EmailSafetyGuard.ResolveEffectiveMode(_env.EnvironmentName, _options));

        try
        {
            await ResetStuckSendingAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Email outbox: failed to reset rows stuck in 'sending'");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in Email Outbox Worker iteration");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Email Outbox Worker stopping at {Time}", DateTimeOffset.Now);
    }

    private async Task ResetStuckSendingAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var reset = await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_outbox SET status = 'queued', updated_at = {0} WHERE status = 'sending' AND updated_at < {1}",
            now, now - StuckSendingAfter);
        if (reset > 0)
        {
            _logger.LogWarning("Email outbox: reset {Count} row(s) stuck in 'sending' for over {Minutes} minutes back to 'queued'",
                reset, (int)StuckSendingAfter.TotalMinutes);
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        var mode = EmailSafetyGuard.ResolveEffectiveMode(_env.EnvironmentName, _options);
        if (mode == EmailMode.Disabled)
        {
            LogNotSendingOncePerMinute("effective mode is Disabled (configured {Mode}, environment {Env}); rows stay queued", _options.Mode, _env.EnvironmentName);
            return;
        }

        if (!IsSmtpConfigured())
        {
            LogNotSendingOncePerMinute("SMTP is not fully configured (host/from address/username/password); rows stay queued");
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        var sentLastHour = Convert.ToInt32(await ScalarAsync(db,
            "SELECT COUNT(*) FROM email_outbox WHERE sent_at IS NOT NULL AND sent_at > @p0", ct, now.AddHours(-1)));
        var budget = _options.MaxPerHour - sentLastHour;
        if (budget <= 0)
        {
            LogNotSendingOncePerMinute("hourly limit of {Max} reached; rows stay queued", _options.MaxPerHour);
            return;
        }

        var ids = await QueryIdsAsync(db, now, Math.Min(BatchSize, budget), ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            await SendOneAsync(id, ct);
        }
    }

    private bool IsSmtpConfigured() =>
        !string.IsNullOrWhiteSpace(_options.SmtpHost)
        && !string.IsNullOrWhiteSpace(_options.FromAddress)
        && !string.IsNullOrWhiteSpace(_options.Username)
        && !string.IsNullOrWhiteSpace(_options.Password);

    private void LogNotSendingOncePerMinute(string reason, params object?[] args)
    {
        var now = DateTime.UtcNow;
        if (now - _lastNotSendingLogUtc < NotSendingLogInterval)
        {
            return;
        }

        _lastNotSendingLogUtc = now;
        _logger.LogWarning("Email outbox not sending: " + reason, args);
    }

    private async Task SendOneAsync(int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Atomic claim: only the caller that flips queued -> sending proceeds.
        var claimed = await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_outbox SET status = 'sending', attempts = attempts + 1, updated_at = {1} WHERE id = {0} AND status = 'queued'",
            id, DateTime.UtcNow);
        if (claimed != 1)
        {
            return;
        }

        EmailOutbox? row = null;
        try
        {
            row = await LoadRowAsync(db, id, ct) ?? throw new InvalidOperationException($"email_outbox row {id} vanished after claim");
            var attachmentRows = await LoadAttachmentsAsync(db, id, ct);

            var delivery = EmailSafetyGuard.Apply(_env.EnvironmentName, _options, row.ToAddresses, row.CcAddresses, row.Subject);
            if (delivery.EffectiveMode == EmailMode.Disabled)
            {
                // Configuration flipped between the poll check and now: give the claim back, do not count the attempt.
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE email_outbox SET status = 'queued', attempts = attempts - 1, updated_at = {1} WHERE id = {0}",
                    id, DateTime.UtcNow);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var fileStore = scope.ServiceProvider.GetRequiredService<IFileStore>();
            var streams = new List<Stream>();
            MimeMessage message;
            try
            {
                var contents = new List<EmailAttachmentContent>();
                foreach (var a in attachmentRows)
                {
                    var stream = await fileStore.OpenAsync(a.FileId, ct);
                    streams.Add(stream);
                    contents.Add(new EmailAttachmentContent(a.FileName, a.ContentType, stream));
                }

                message = EmailMessageBuilder.Build(_options, row, delivery, contents);
                await SendSmtpAsync(message, ct);
            }
            finally
            {
                foreach (var s in streams)
                {
                    await s.DisposeAsync();
                }
            }

            var sentAt = DateTime.UtcNow;
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE email_outbox SET status = 'sent', sent_at = {1}, message_id = {2}, last_error = NULL, " +
                "to_addresses = {3}, cc_addresses = {4}, subject = {5}, original_to = {6}, updated_at = {1} WHERE id = {0}",
                new object?[] { id, sentAt, message.MessageId, delivery.To, delivery.Cc, delivery.Subject, delivery.OriginalTo });
            _logger.LogInformation("Email outbox {Id} sent (mode {Mode}, attempt {Attempt})", id, delivery.EffectiveMode, row.Attempts);

            await AppendToSentFolderAsync(id, message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // shutdown mid-send: row stays 'sending' and is reset on next startup after 15 minutes
        }
        catch (Exception ex)
        {
            await RecordFailureAsync(db, id, row?.Attempts ?? 1, ex, ct);
        }
    }

    private async Task SendSmtpAsync(MimeMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient { Timeout = NetworkTimeoutMs };
        await client.ConnectAsync(
            _options.SmtpHost, _options.SmtpPort,
            _options.SmtpSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(_options.Username, _options.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>Copies the sent message to the IMAP Sent folder. Failure is a warning only — the mail stays 'sent'.</summary>
    private async Task AppendToSentFolderAsync(int id, MimeMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ImapHost))
        {
            return;
        }

        try
        {
            using var imap = new ImapClient { Timeout = NetworkTimeoutMs };
            await imap.ConnectAsync(_options.ImapHost, _options.ImapPort, SecureSocketOptions.SslOnConnect, ct);
            await imap.AuthenticateAsync(_options.Username, _options.Password, ct);
            var folder = imap.GetFolder(_options.SentFolder);
            await folder.OpenAsync(FolderAccess.ReadWrite, ct);
            await folder.AppendAsync(message, MessageFlags.Seen, ct);
            await imap.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Email outbox {Id}: IMAP append to '{Folder}' failed; mail stays 'sent'", id, _options.SentFolder);
        }
    }

    private async Task RecordFailureAsync(NordicBeesERPContext db, int id, int attempts, Exception ex, CancellationToken ct)
    {
        var error = $"{ex.GetType().Name}: {ex.Message}";
        if (error.Length > LastErrorMaxLength)
        {
            error = error[..LastErrorMaxLength];
        }

        var now = DateTime.UtcNow;
        var delay = EmailMessageBuilder.NextAttemptDelay(attempts);
        if (delay is null)
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE email_outbox SET status = 'failed', last_error = {1}, updated_at = {2} WHERE id = {0}",
                id, error, now);
            _logger.LogError(ex, "Email outbox {Id} failed permanently after {Attempts} attempts", id, attempts);
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE email_outbox SET status = 'queued', last_error = {1}, next_attempt_at = {2}, updated_at = {3} WHERE id = {0}",
                id, error, now + delay.Value, now);
            _logger.LogWarning(ex, "Email outbox {Id} attempt {Attempt} failed; retry in {Delay}", id, attempts, delay.Value);
        }
    }

    // ---- raw reads (global NoTracking, no DbSet for the outbox tables) ----

    private static DbParameter Param(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        return p;
    }

    private static async Task<object?> ScalarAsync(NordicBeesERPContext db, string sql, CancellationToken ct, params object?[] args)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != System.Data.ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (var i = 0; i < args.Length; i++) cmd.Parameters.Add(Param(cmd, $"@p{i}", args[i]));
            return await cmd.ExecuteScalarAsync(ct);
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }

    private static async Task<List<int>> QueryIdsAsync(NordicBeesERPContext db, DateTime now, int limit, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != System.Data.ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM email_outbox WHERE status = 'queued' AND next_attempt_at <= @p0 ORDER BY next_attempt_at, id LIMIT @p1";
            cmd.Parameters.Add(Param(cmd, "@p0", now));
            cmd.Parameters.Add(Param(cmd, "@p1", limit));
            var ids = new List<int>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) ids.Add(Convert.ToInt32(reader.GetValue(0)));
            return ids;
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }

    private static string? Str(DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col)) ? null : Convert.ToString(r.GetValue(r.GetOrdinal(col)));

    private static async Task<EmailOutbox?> LoadRowAsync(NordicBeesERPContext db, int id, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != System.Data.ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, to_addresses, cc_addresses, reply_to, subject, body_html, body_text, attempts FROM email_outbox WHERE id = @p0";
            cmd.Parameters.Add(Param(cmd, "@p0", id));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            return new EmailOutbox
            {
                Id = Convert.ToInt32(r.GetValue(r.GetOrdinal("id"))),
                ToAddresses = Str(r, "to_addresses") ?? string.Empty,
                CcAddresses = Str(r, "cc_addresses"),
                ReplyTo = Str(r, "reply_to"),
                Subject = Str(r, "subject") ?? string.Empty,
                BodyHtml = Str(r, "body_html") ?? string.Empty,
                BodyText = Str(r, "body_text"),
                Attempts = Convert.ToInt32(r.GetValue(r.GetOrdinal("attempts")))
            };
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }

    private static async Task<List<EmailOutboxAttachment>> LoadAttachmentsAsync(NordicBeesERPContext db, int outboxId, CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != System.Data.ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, file_name, content_type, file_id, size_bytes FROM email_outbox_attachments WHERE outbox_id = @p0 ORDER BY id";
            cmd.Parameters.Add(Param(cmd, "@p0", outboxId));
            var list = new List<EmailOutboxAttachment>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                list.Add(new EmailOutboxAttachment
                {
                    Id = Convert.ToInt32(r.GetValue(0)),
                    OutboxId = outboxId,
                    FileName = Str(r, "file_name") ?? string.Empty,
                    ContentType = Str(r, "content_type") ?? "application/octet-stream",
                    FileId = Convert.ToInt64(r.GetValue(3)),
                    SizeBytes = Convert.ToInt32(r.GetValue(4))
                });
            }
            return list;
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }
}
