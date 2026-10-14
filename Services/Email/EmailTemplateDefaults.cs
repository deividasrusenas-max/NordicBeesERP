namespace NordicBeesERP.Services.Email;

/// <summary>
/// Built-in copies of the seeded e-mail templates (identical to the INSERT IGNORE rows of Migrations/Scripts/20261014_dunning.sql).
/// Used when the email_templates table is not applied yet, or a (code, language) row is missing/inactive.
/// </summary>
public static class EmailTemplateDefaults
{
    public static readonly IReadOnlyList<EmailTemplate> All = new List<EmailTemplate>
    {
        new("INVOICE_SEND", "LT", @"PVM sąskaita faktūra {{InvoiceNumber}}", @"<p>Gerbiamieji,</p><p>siunčiame sąskaitą faktūrą <strong>{{InvoiceNumber}}</strong> ({{InvoiceDate}}) už {{Total}}. Apmokėjimo terminas – {{DueDate}}.</p><p>Sąskaita pridėta PDF formatu.</p>{{CompanySignature}}"),
        new("INVOICE_SEND", "EN", @"Invoice {{InvoiceNumber}}", @"<p>Dear {{PartnerName}},</p><p>Please find attached invoice <strong>{{InvoiceNumber}}</strong> dated {{InvoiceDate}} for {{Total}}. The payment due date is {{DueDate}}.</p>{{CompanySignature}}"),
        new("CREDIT_NOTE_SEND", "LT", @"Kreditinė sąskaita {{InvoiceNumber}}", @"<p>Gerbiamieji,</p><p>siunčiame kreditinę sąskaitą <strong>{{InvoiceNumber}}</strong> ({{InvoiceDate}}) už {{Total}}.</p><p>Dokumentas pridėtas PDF formatu.</p>{{CompanySignature}}"),
        new("CREDIT_NOTE_SEND", "EN", @"Credit note {{InvoiceNumber}}", @"<p>Dear {{PartnerName}},</p><p>Please find attached credit note <strong>{{InvoiceNumber}}</strong> dated {{InvoiceDate}} for {{Total}}.</p>{{CompanySignature}}"),
        new("REPORT_SEND", "LT", @"{{ReportName}} {{Period}}", @"<p>Gerbiamieji,</p><p>siunčiame ataskaitą „{{ReportName}}“ ({{Period}}).</p><p>Ataskaita pridėta prie šio laiško.</p>{{CompanySignature}}"),
        new("REPORT_SEND", "EN", @"{{ReportName}} {{Period}}", @"<p>Dear {{PartnerName}},</p><p>Please find attached the report “{{ReportName}}” ({{Period}}).</p>{{CompanySignature}}"),
        new("DUNNING_L1", "LT", @"Priminimas dėl neapmokėtų sąskaitų", @"<p>Gerbiamieji,</p><p>primename, kad žemiau išvardytų sąskaitų apmokėjimo terminas jau suėjo. Jei apmokėjimą jau atlikote, prašome šio laiško nepaisyti.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Jei turite klausimų, mielai padėsime.</p>{{CompanySignature}}"),
        new("DUNNING_L1", "EN", @"Reminder: overdue invoices", @"<p>Dear {{PartnerName}},</p><p>This is a friendly reminder that the payment due date of the invoices listed below has passed. If you have already made the payment, please disregard this message.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please let us know if you have any questions.</p>{{CompanySignature}}"),
        new("DUNNING_L2", "LT", @"Antrasis priminimas dėl pradelstų sąskaitų", @"<p>Gerbiamieji,</p><p>nepaisant ankstesnio priminimo, žemiau išvardytų sąskaitų apmokėjimo negavome.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Prašome apmokėti per 7 dienas arba informuoti mus apie galimas priežastis, trukdančias tai padaryti.</p>{{CompanySignature}}"),
        new("DUNNING_L2", "EN", @"Second reminder: overdue invoices", @"<p>Dear {{PartnerName}},</p><p>Despite our previous reminder, we have not received payment for the invoices listed below.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please arrange payment within 7 days or let us know if anything prevents you from doing so.</p>{{CompanySignature}}"),
        new("DUNNING_L3", "LT", @"Paskutinis priminimas dėl pradelstų sąskaitų", @"<p>Gerbiamieji,</p><p>tai paskutinis priminimas dėl žemiau išvardytų pradelstų sąskaitų.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Prašome sumokėti per 5 darbo dienas. Jei apmokėjimas jau atliktas arba yra neaiškumų dėl sąskaitų, prašome nedelsiant su mumis susisiekti.</p>{{InterestBlock}}{{CompanySignature}}"),
        new("DUNNING_L3", "EN", @"Final reminder: overdue invoices", @"<p>Dear {{PartnerName}},</p><p>This is our final reminder regarding the overdue invoices listed below.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please make the payment within 5 business days. If payment has already been made or anything about the invoices is unclear, please contact us right away.</p>{{InterestBlock}}{{CompanySignature}}"),
    };

    public static EmailTemplate? Find(string code, string language) =>
        All.FirstOrDefault(t => t.Code == code && t.Language == language);
}
