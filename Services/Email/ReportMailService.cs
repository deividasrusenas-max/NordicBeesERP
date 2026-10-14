using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Dunning;

namespace NordicBeesERP.Services.Email;

/// <summary>One attachment offered by the generic send dialog; the bytes are produced only when the attachment is selected and sent.</summary>
public sealed record EmailAttachmentSpec(string Name, string ContentType, Func<Task<byte[]>> Factory);

public sealed record ReportMailPartner(string Name, string To, bool NoAddress, bool NoEmailFlag);

/// <summary>Prepares and queues "send by e-mail" messages for generated reports (template REPORT_SEND, related_type 'report').</summary>
public class ReportMailService
{
    public const string TemplateCode = "REPORT_SEND";
    public const string RelatedTypeReport = "report";

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IEmailTemplateService _templates;
    private readonly IEmailQueue _queue;
    private readonly ICompanySettingsService _company;

    public ReportMailService(
        IDbContextFactory<NordicBeesERPContext> dbFactory, IEmailTemplateService templates, IEmailQueue queue, ICompanySettingsService company)
    {
        _dbFactory = dbFactory;
        _templates = templates;
        _queue = queue;
        _company = company;
    }

    /// <summary>Partner recipient rule: COALESCE(invoice_email, email), ';' lists allowed. Null partner -> empty address.</summary>
    public async Task<ReportMailPartner> GetPartnerAsync(int? partnerId, CancellationToken ct = default)
    {
        if (partnerId is null) return new ReportMailPartner(string.Empty, string.Empty, false, false);
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var rows = await RawSql.QueryAsync(db,
            "SELECT name, COALESCE(NULLIF(TRIM(invoice_email), ''), NULLIF(TRIM(email), '')) AS recipient, no_email FROM business_partners WHERE id = @p0",
            r => (Name: r.Str("name") ?? string.Empty, Recipient: r.Str("recipient"), NoEmail: r.Bool("no_email")),
            ct, partnerId.Value);
        if (rows.Count == 0) return new ReportMailPartner(string.Empty, string.Empty, false, false);
        var to = DunningEngine.ResolveRecipient(null, rows[0].Recipient) ?? string.Empty;
        return new ReportMailPartner(rows[0].Name, to, string.IsNullOrWhiteSpace(to), rows[0].NoEmail);
    }

    public async Task<(string Subject, string BodyHtml)> RenderAsync(
        string templateCode, string language, string partnerName, string reportName, string period, CancellationToken ct = default)
    {
        var lang = DunningLanguage.Normalize(language);
        var template = await _templates.GetAsync(templateCode, lang, ct);
        var company = await _company.GetSettingsAsync();
        return RenderTemplate(template, lang, partnerName, reportName, period, company.CompanyName, company.Phone, company.Email);
    }

    /// <summary>Pure rendering step (unit-tested).</summary>
    public static (string Subject, string BodyHtml) RenderTemplate(
        EmailTemplate template, string language, string partnerName, string reportName, string period,
        string? companyName, string? phone, string? email)
    {
        var plain = new Dictionary<string, string>
        {
            ["PartnerName"] = string.IsNullOrWhiteSpace(partnerName) ? (language == "LT" ? "kliente" : "customer") : partnerName,
            ["ReportName"] = reportName,
            ["Period"] = period
        };
        var html = new Dictionary<string, string>
        {
            ["CompanySignature"] = EmailFormat.Signature(language, companyName, phone, email)
        };
        return EmailTemplateRenderer.Render(template, plain, html);
    }

    /// <summary>Builds the selected attachments and queues the message. Returns the email_outbox id. Throws ArgumentException for bad addresses or no attachments.</summary>
    public async Task<int> SendAsync(
        string to, string? cc, string subject, string bodyHtml, IReadOnlyList<EmailAttachmentSpec> selected,
        string relatedType, int? relatedId, CancellationToken ct = default)
    {
        if (selected.Count == 0) throw new ArgumentException("At least one attachment is required.", nameof(selected));
        var toList = DunningEngine.SplitAddresses(to);
        var ccList = DunningEngine.SplitAddresses(cc);
        EmailQueue.NormalizeAddressList(toList, "To", required: true);
        EmailQueue.NormalizeAddressList(ccList, "Cc", required: false);

        var attachments = new List<EmailAttachmentInput>();
        foreach (var a in selected)
        {
            attachments.Add(new EmailAttachmentInput { Content = await a.Factory(), FileName = a.Name, ContentType = a.ContentType });
        }

        return await _queue.EnqueueAsync(new EmailRequest
        {
            To = toList,
            Cc = ccList.Count == 0 ? null : ccList,
            Subject = subject,
            BodyHtml = bodyHtml,
            Attachments = attachments,
            RelatedType = relatedType,
            RelatedId = relatedId
        }, ct);
    }
}
