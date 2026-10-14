using System.Globalization;
using System.Net;
using System.Text;
using NordicBeesERP.Services.Dunning;

namespace NordicBeesERP.Services.Email;

/// <summary>Pure formatting + HTML building for outgoing e-mails (LT/EN). Everything user-derived is HTML-encoded.</summary>
public static class EmailFormat
{
    /// <summary>"1 234,56 €" (non-breaking space group separator, comma decimals) — independent of the container's ICU/culture data.</summary>
    public static string Money(decimal amount)
    {
        var s = Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
        s = s.Replace(",", " ").Replace(".", ",");
        return (amount < 0 ? "-" : string.Empty) + s + " €";
    }

    public static string Date(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Signature(string language, string? companyName, string? phone, string? email)
    {
        var lt = language == "LT";
        var sb = new StringBuilder("<p>");
        sb.Append(lt ? "Pagarbiai," : "Kind regards,");
        foreach (var line in new[] { companyName, phone, email })
        {
            if (!string.IsNullOrWhiteSpace(line)) sb.Append("<br>").Append(WebUtility.HtmlEncode(line.Trim()));
        }

        return sb.Append("</p>").ToString();
    }

    public static string InvoiceTable(string language, IEnumerable<DunningInvoiceProposal> invoices)
    {
        var lt = language == "LT";
        const string th = "border:1px solid #cbd5e1;padding:6px 10px;background:#f1f5f9;text-align:left";
        const string td = "border:1px solid #cbd5e1;padding:6px 10px";
        const string tdr = "border:1px solid #cbd5e1;padding:6px 10px;text-align:right;white-space:nowrap";
        var sb = new StringBuilder("<table style=\"border-collapse:collapse;font-size:14px\"><tr>");
        foreach (var h in lt
                     ? new[] { "Sąskaita", "Data", "Terminas", "Pradelsta dienų", "Likusi suma" }
                     : new[] { "Invoice", "Date", "Due date", "Days overdue", "Amount due" })
        {
            sb.Append("<th style=\"").Append(th).Append("\">").Append(h).Append("</th>");
        }

        sb.Append("</tr>");
        foreach (var i in invoices)
        {
            sb.Append("<tr><td style=\"").Append(td).Append("\">").Append(WebUtility.HtmlEncode(i.InvoiceNumber)).Append("</td>")
              .Append("<td style=\"").Append(td).Append("\">").Append(Date(i.InvoiceDate)).Append("</td>")
              .Append("<td style=\"").Append(td).Append("\">").Append(Date(i.DueDate)).Append("</td>")
              .Append("<td style=\"").Append(tdr).Append("\">").Append(i.DaysOverdue).Append("</td>")
              .Append("<td style=\"").Append(tdr).Append("\">").Append(Money(i.Remaining)).Append("</td></tr>");
        }

        return sb.Append("</table>").ToString();
    }

    /// <summary>
    /// Informational statutory-interest / fixed-fee paragraph. Empty unless the config shows at least one of them.
    /// Wording is deliberately conditional ("may be charged") — it informs, it does not demand.
    /// </summary>
    public static string InterestBlock(string language, IReadOnlyList<DunningInvoiceProposal> invoices, DunningConfig cfg)
    {
        var interest = cfg.ShowInterest && cfg.InterestRatePct is not null ? invoices.Sum(i => i.InterestEur ?? 0m) : (decimal?)null;
        var fee = cfg.ShowFixedFee ? invoices.Sum(i => i.FixedFeeEur ?? 0m) : (decimal?)null;
        if (interest is null && fee is null) return string.Empty;

        var lt = language == "LT";
        var parts = new List<string>();
        if (interest is not null)
        {
            parts.Add(lt
                ? $"{cfg.InterestRatePct!.Value.ToString("0.##", CultureInfo.InvariantCulture)} % metinių palūkanų už pavėluotą apmokėjimą (šiuo metu apie {Money(interest.Value)})"
                : $"late-payment interest at {cfg.InterestRatePct!.Value.ToString("0.##", CultureInfo.InvariantCulture)} % per year (currently about {Money(interest.Value)})");
        }

        if (fee is not null)
        {
            parts.Add(lt
                ? $"fiksuotos {Money(cfg.FixedFeeEur)} dydžio išieškojimo išlaidų kompensacijos už kiekvieną pradelstą sąskaitą (iš viso {Money(fee.Value)})"
                : $"a fixed recovery-cost compensation of {Money(cfg.FixedFeeEur)} per overdue invoice ({Money(fee.Value)} in total)");
        }

        var joined = string.Join(lt ? " ir " : " and ", parts);
        return lt
            ? $"<p>Informuojame, kad pagal galiojančius teisės aktus gali būti skaičiuojamos {joined}. Tai tik informacija – jokių papildomų sumų šiuo metu nereikalaujame.</p>"
            : $"<p>Please note that, under applicable law, {joined} may be charged. This is for information only – we are not requesting any additional amounts at this time.</p>";
    }
}
