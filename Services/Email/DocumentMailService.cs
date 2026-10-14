using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Dunning;

namespace NordicBeesERP.Services.Email;

public static class DocumentMailKind
{
    public const string Invoice = "invoice";
    public const string CreditNote = "credit_note";
}

public sealed record DocumentMailDraft(
    string Kind, int DocumentId, int PartnerId, string PartnerName, string DocumentNumber, string To, string? Cc,
    string Subject, string BodyHtml, string AttachmentName, string Language, bool NoAddress, bool NoEmailFlag, bool IsDraftDocument);

/// <summary>Prepares and queues "send by e-mail" messages for a sales invoice or a credit note (template INVOICE_SEND / CREDIT_NOTE_SEND, PDF attached).</summary>
public class DocumentMailService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
    private readonly IEmailTemplateService _templates;
    private readonly IEmailQueue _queue;
    private readonly ICompanySettingsService _company;
    private readonly IInvoiceService _invoices;
    private readonly ICreditNoteService _creditNotes;

    public DocumentMailService(
        IDbContextFactory<NordicBeesERPContext> dbFactory, IEmailTemplateService templates, IEmailQueue queue,
        ICompanySettingsService company, IInvoiceService invoices, ICreditNoteService creditNotes)
    {
        _dbFactory = dbFactory;
        _templates = templates;
        _queue = queue;
        _company = company;
        _invoices = invoices;
        _creditNotes = creditNotes;
    }

    private sealed record DocRow(string Number, DateOnly Date, DateOnly? Due, decimal Total, int PartnerId, string Status);

    private async Task<DocRow?> LoadDocAsync(string kind, int id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var sql = kind == DocumentMailKind.Invoice
            ? "SELECT invoice_number AS number, invoice_date AS doc_date, COALESCE(due_date, payment_due_date) AS due, total_incl_vat AS total, customer_id, status FROM invoices WHERE id = @p0"
            : "SELECT credit_note_number AS number, credit_date AS doc_date, NULL AS due, total_incl_vat AS total, customer_id, status FROM credit_notes WHERE id = @p0";
        var rows = await RawSql.QueryAsync(db, sql,
            r => new DocRow(r.Str("number") ?? string.Empty, r.Date("doc_date"), r.DateOrNull("due"), r.Dec("total"), r.Int("customer_id"), r.Str("status") ?? string.Empty),
            ct, id);
        return rows.FirstOrDefault();
    }

    public async Task<DocumentMailDraft> PrepareAsync(string kind, int id, CancellationToken ct = default)
    {
        var doc = await LoadDocAsync(kind, id, ct) ?? throw new InvalidOperationException("Document not found.");
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var partners = await RawSql.QueryAsync(db,
            "SELECT name, COALESCE(NULLIF(TRIM(invoice_email), ''), NULLIF(TRIM(email), '')) AS recipient, default_language, no_email FROM business_partners WHERE id = @p0",
            r => (Name: r.Str("name") ?? string.Empty, Recipient: r.Str("recipient"), Lang: r.Str("default_language"), NoEmail: r.Bool("no_email")),
            ct, doc.PartnerId);
        var partner = partners.FirstOrDefault();
        var language = DunningLanguage.Normalize(partner.Lang);
        var company = await _company.GetSettingsAsync();

        var template = await _templates.GetAsync(kind == DocumentMailKind.Invoice ? "INVOICE_SEND" : "CREDIT_NOTE_SEND", language, ct);
        var plain = new Dictionary<string, string>
        {
            ["PartnerName"] = partner.Name,
            ["InvoiceNumber"] = doc.Number,
            ["InvoiceDate"] = EmailFormat.Date(doc.Date),
            ["DueDate"] = doc.Due is null ? string.Empty : EmailFormat.Date(doc.Due.Value),
            ["Total"] = EmailFormat.Money(doc.Total),
            ["TotalOverdue"] = string.Empty
        };
        var html = new Dictionary<string, string>
        {
            ["CompanySignature"] = EmailFormat.Signature(language, company.CompanyName, company.Phone, company.Email),
            ["InvoiceTable"] = string.Empty,
            ["InterestBlock"] = string.Empty
        };
        var (subject, body) = EmailTemplateRenderer.Render(template, plain, html);

        var to = DunningEngine.ResolveRecipient(null, partner.Recipient) ?? string.Empty;
        var isDraft = string.Equals(doc.Status, "draft", StringComparison.OrdinalIgnoreCase);
        return new DocumentMailDraft(kind, id, doc.PartnerId, partner.Name, doc.Number, to, null, subject, body,
            DunningService.SafeFileName(doc.Number) + ".pdf", language, string.IsNullOrWhiteSpace(to), partner.NoEmail, isDraft);
    }

    /// <summary>Generates the (frozen) PDF and queues the message. Returns the email_outbox id. Throws ArgumentException for bad addresses.</summary>
    public async Task<int> SendAsync(
        string kind, int id, string to, string? cc, string subject, string bodyHtml, CancellationToken ct = default)
    {
        var doc = await LoadDocAsync(kind, id, ct) ?? throw new InvalidOperationException("Document not found.");
        if (string.Equals(doc.Status, "draft", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Draft documents cannot be e-mailed.");

        var toList = DunningEngine.SplitAddresses(to);
        var ccList = DunningEngine.SplitAddresses(cc);
        // Validate before the (expensive) PDF generation; EnqueueAsync validates again.
        EmailQueue.NormalizeAddressList(toList, "To", required: true);
        EmailQueue.NormalizeAddressList(ccList, "Cc", required: false);

        var pdf = kind == DocumentMailKind.Invoice
            ? await _invoices.GenerateAndSavePdfAsync(id)
            : await _creditNotes.GenerateAndSavePdfAsync(id);

        return await _queue.EnqueueAsync(new EmailRequest
        {
            To = toList,
            Cc = ccList.Count == 0 ? null : ccList,
            Subject = subject,
            BodyHtml = bodyHtml,
            Attachments = new[]
            {
                new EmailAttachmentInput { Content = pdf, FileName = DunningService.SafeFileName(doc.Number) + ".pdf", ContentType = "application/pdf" }
            },
            RelatedType = kind,
            RelatedId = id
        }, ct);
    }
}
