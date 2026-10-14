using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services.Email;

public sealed record EmailTemplateRow(string Code, string Language, string Subject, string BodyHtml, bool IsActive, bool IsBuiltIn);

public interface IEmailTemplateService
{
    /// <summary>Active DB row for (code, language); else the built-in default; else the same code in EN. Never throws for known codes.</summary>
    Task<EmailTemplate> GetAsync(string code, string language, CancellationToken ct = default);

    /// <summary>All rows for the editor: DB rows plus built-in defaults that have no DB row yet (IsBuiltIn = true).</summary>
    Task<List<EmailTemplateRow>> ListAsync(CancellationToken ct = default);

    Task SaveAsync(EmailTemplateRow row, CancellationToken ct = default);
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(IDbContextFactory<NordicBeesERPContext> dbFactory, ILogger<EmailTemplateService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<EmailTemplate> GetAsync(string code, string language, CancellationToken ct = default)
    {
        var lang = DunningLanguageOrDefault(language);
        var rows = await TryLoadAsync(ct);
        EmailTemplate? Pick(string l)
        {
            var db = rows?.FirstOrDefault(r => r.Code == code && r.Language == l && r.IsActive);
            return db is not null ? new EmailTemplate(db.Code, db.Language, db.Subject, db.BodyHtml) : EmailTemplateDefaults.Find(code, l);
        }

        // A template switched off in the editor must not silently fall back to the built-in text: refuse to build the message.
        if (rows?.Any(r => r.Code == code && r.Language == lang && !r.IsActive) == true)
            throw new InvalidOperationException($"E-mail template '{code}' ({lang}) is inactive.");

        return Pick(lang) ?? Pick("EN") ?? throw new InvalidOperationException($"E-mail template '{code}' does not exist.");
    }

    public async Task<List<EmailTemplateRow>> ListAsync(CancellationToken ct = default)
    {
        var rows = await TryLoadAsync(ct) ?? new List<EmailTemplateRow>();
        foreach (var d in EmailTemplateDefaults.All)
        {
            if (!rows.Any(r => r.Code == d.Code && r.Language == d.Language))
            {
                rows.Add(new EmailTemplateRow(d.Code, d.Language, d.Subject, d.BodyHtml, true, true));
            }
        }

        return rows.OrderBy(r => r.Code, StringComparer.Ordinal).ThenBy(r => r.Language, StringComparer.Ordinal).ToList();
    }

    public async Task SaveAsync(EmailTemplateRow row, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(row.Subject) || string.IsNullOrWhiteSpace(row.BodyHtml))
            throw new ArgumentException("Subject and body are required.", nameof(row));
        if (row.Subject.Length > 500)
            throw new ArgumentException("Subject is longer than 500 characters.", nameof(row));

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO email_templates (code, language, subject, body_html, is_active, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}) " +
            "ON DUPLICATE KEY UPDATE subject = VALUES(subject), body_html = VALUES(body_html), is_active = VALUES(is_active), updated_at = VALUES(updated_at)",
            new object?[] { row.Code, row.Language, row.Subject, row.BodyHtml, row.IsActive ? 1 : 0, DateTime.UtcNow });
    }

    private static string DunningLanguageOrDefault(string language) =>
        string.Equals(language, "LT", StringComparison.OrdinalIgnoreCase) ? "LT" : "EN";

    /// <summary>Null when the table is not applied yet (built-in defaults are used then).</summary>
    private async Task<List<EmailTemplateRow>?> TryLoadAsync(CancellationToken ct)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            return await RawSql.QueryAsync(db, "SELECT code, language, subject, body_html, is_active FROM email_templates",
                r => new EmailTemplateRow(r.Str("code")!, r.Str("language")!, r.Str("subject")!, r.Str("body_html")!, r.Bool("is_active"), false), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("email_templates not readable ({Message}); using built-in templates", ex.Message);
            return null;
        }
    }
}
