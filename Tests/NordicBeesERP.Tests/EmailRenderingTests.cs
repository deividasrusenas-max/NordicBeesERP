using NordicBeesERP.Services.Dunning;
using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Tests;

public class EmailRenderingTests
{
    private static readonly DateOnly D = new(2026, 10, 14);

    [Fact]
    public void Renderer_encodes_plain_values_in_body_but_not_html_values()
    {
        var t = new EmailTemplate("X", "LT", "Hello {{PartnerName}}", "<p>{{PartnerName}}</p>{{InvoiceTable}}");
        var (subject, body) = EmailTemplateRenderer.Render(t,
            new Dictionary<string, string> { ["PartnerName"] = "A&B <script>" },
            new Dictionary<string, string> { ["InvoiceTable"] = "<table></table>" });
        Assert.Equal("Hello A&B <script>", subject);
        Assert.Equal("<p>A&amp;B &lt;script&gt;</p><table></table>", body);
    }

    [Fact]
    public void Renderer_strips_newlines_from_subject_and_drops_html_values_and_unknowns()
    {
        var t = new EmailTemplate("X", "EN", "S {{PartnerName}} {{InvoiceTable}} {{Nope}}", "{{Nope}}x");
        var (subject, body) = EmailTemplateRenderer.Render(t,
            new Dictionary<string, string> { ["PartnerName"] = "a\r\nBcc: evil@x.test" },
            new Dictionary<string, string> { ["InvoiceTable"] = "<b>t</b>" });
        Assert.DoesNotContain('\n', subject);
        Assert.DoesNotContain('\r', subject);
        Assert.DoesNotContain("<b>", subject);
        Assert.Equal("x", body);
    }

    [Fact]
    public void Every_builtin_template_only_uses_known_placeholders_and_exists_in_both_languages()
    {
        var known = new HashSet<string>(EmailTemplateRenderer.Placeholders);
        foreach (var t in EmailTemplateDefaults.All)
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(t.Subject + t.BodyHtml, @"\{\{(\w+)\}\}"))
                Assert.Contains(m.Groups[1].Value, known);
        }

        foreach (var code in new[] { "INVOICE_SEND", "CREDIT_NOTE_SEND", "DUNNING_L1", "DUNNING_L2", "DUNNING_L3", "REPORT_SEND" })
        {
            Assert.NotNull(EmailTemplateDefaults.Find(code, "LT"));
            Assert.NotNull(EmailTemplateDefaults.Find(code, "EN"));
        }
    }

    [Fact]
    public void Only_the_final_dunning_template_carries_the_interest_block()
    {
        Assert.Contains("{{InterestBlock}}", EmailTemplateDefaults.Find("DUNNING_L3", "LT")!.BodyHtml);
        Assert.DoesNotContain("{{InterestBlock}}", EmailTemplateDefaults.Find("DUNNING_L1", "LT")!.BodyHtml);
        Assert.DoesNotContain("{{InterestBlock}}", EmailTemplateDefaults.Find("DUNNING_L2", "EN")!.BodyHtml);
    }

    [Theory]
    [InlineData("1234.5", "1 234,50 €")]
    [InlineData("0", "0,00 €")]
    [InlineData("-12.345", "-12,35 €")]
    public void Money_format(string amount, string expected)
    {
        Assert.Equal(expected, EmailFormat.Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static DunningInvoiceProposal Prop(string number, decimal remaining, decimal? interest = null, decimal? fee = null) =>
        new(1, number, D.AddDays(-40), D.AddDays(-10), 10, remaining, remaining, 1, null, null, interest, fee);

    [Fact]
    public void Invoice_table_encodes_numbers_and_has_localised_headers()
    {
        var lt = EmailFormat.InvoiceTable("LT", new[] { Prop("A<1>", 10m) });
        Assert.Contains("A&lt;1&gt;", lt);
        Assert.Contains("Likusi suma", lt);
        Assert.Contains("Amount due", EmailFormat.InvoiceTable("EN", new[] { Prop("A", 10m) }));
    }

    [Fact]
    public void Interest_block_is_empty_unless_enabled_and_sums_when_enabled()
    {
        var items = new[] { Prop("A", 100m, 1.5m, 40m), Prop("B", 100m, 2.5m, 40m) };
        Assert.Equal(string.Empty, EmailFormat.InterestBlock("LT", items, new DunningConfig()));
        Assert.Equal(string.Empty, EmailFormat.InterestBlock("LT", items, new DunningConfig { ShowInterest = true, InterestRatePct = null }));

        var both = EmailFormat.InterestBlock("EN", items, new DunningConfig { ShowInterest = true, InterestRatePct = 8m, ShowFixedFee = true, FixedFeeEur = 40m });
        Assert.Contains("4,00", both);   // 1.5 + 2.5
        Assert.Contains("80,00", both);  // 2 × 40
        Assert.Contains("for information only", both);
    }

    [Theory]
    [InlineData("LAK2026-0001", "LAK2026-0001")]
    [InlineData("LAK 2026/1", "LAK_2026_1")]
    [InlineData("Čia/įmonė", "ia__mon")]
    [InlineData("", "document")]
    public void Safe_file_name(string raw, string expected) => Assert.Equal(expected, DunningService.SafeFileName(raw));

    [Fact]
    public void Open_invoice_sql_encodes_the_agreed_paid_logic()
    {
        var sql = DunningRepository.OpenInvoicesSql;
        Assert.Contains("COALESCE(i.due_date, i.payment_due_date)", sql);
        Assert.Contains("i.invoice_number LIKE 'LAK%' AND i.invoice_number NOT LIKE 'ULAK%'", sql);
        Assert.Contains("i.status NOT IN ('draft', 'cancelled', 'disputed')", sql);
        Assert.Contains("status <> 'disputed'", sql);
        Assert.Contains("i.total_incl_vat - i.paid_amount - IFNULL(c.credited, 0)", sql);
    }
}
