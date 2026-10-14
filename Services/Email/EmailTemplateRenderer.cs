using System.Net;
using System.Text.RegularExpressions;

namespace NordicBeesERP.Services.Email;

/// <summary>
/// Pure {{Placeholder}} substitution. Plain values are HTML-encoded in the body; "HTML" values (InvoiceTable,
/// CompanySignature, InterestBlock — built by us from encoded parts) are inserted as-is. In the subject every value is
/// plain text with CR/LF stripped (no header injection) and HTML values are dropped. Unknown placeholders become empty.
/// </summary>
public static class EmailTemplateRenderer
{
    public static readonly string[] Placeholders =
        { "PartnerName", "InvoiceNumber", "InvoiceDate", "Total", "DueDate", "InvoiceTable", "TotalOverdue", "CompanySignature", "InterestBlock" };

    public static readonly string[] HtmlPlaceholders = { "InvoiceTable", "CompanySignature", "InterestBlock" };

    private static readonly Regex Token = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    public static (string Subject, string BodyHtml) Render(
        EmailTemplate template, IReadOnlyDictionary<string, string> plain, IReadOnlyDictionary<string, string>? html = null)
    {
        html ??= new Dictionary<string, string>();

        var subject = Token.Replace(template.Subject, m =>
        {
            var key = m.Groups[1].Value;
            return plain.TryGetValue(key, out var v) ? Clean(v) : string.Empty;
        });

        var body = Token.Replace(template.BodyHtml, m =>
        {
            var key = m.Groups[1].Value;
            if (html.TryGetValue(key, out var raw)) return raw;
            return plain.TryGetValue(key, out var v) ? WebUtility.HtmlEncode(v) : string.Empty;
        });

        return (Clean(subject), body);
    }

    private static string Clean(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();

    /// <summary>Sample values for the settings-page preview.</summary>
    public static (Dictionary<string, string> Plain, Dictionary<string, string> Html) SampleData(string language)
    {
        var lt = language == "LT";
        var plain = new Dictionary<string, string>
        {
            ["PartnerName"] = "UAB Pavyzdys",
            ["InvoiceNumber"] = "LAK2026-0123",
            ["InvoiceDate"] = "2026-09-01",
            ["Total"] = "1 234,56 €",
            ["DueDate"] = "2026-09-15",
            ["TotalOverdue"] = "1 234,56 €"
        };
        var html = new Dictionary<string, string>
        {
            ["InvoiceTable"] = "<table style=\"border-collapse:collapse\"><tr><td style=\"border:1px solid #ccc;padding:4px 8px\">LAK2026-0123</td><td style=\"border:1px solid #ccc;padding:4px 8px\">1 234,56 €</td></tr></table>",
            ["CompanySignature"] = lt ? "<p>Pagarbiai,<br>Įmonės pavadinimas</p>" : "<p>Kind regards,<br>Company name</p>",
            ["InterestBlock"] = string.Empty
        };
        return (plain, html);
    }
}
