using NordicBeesERP.Services.Dunning;
using NordicBeesERP.Services.Email;
using Xunit;

namespace NordicBeesERP.Tests;

public class ReportMailTests
{
    [Fact]
    public void ReportSend_default_renders_all_placeholders_in_LT()
    {
        var t = EmailTemplateDefaults.Find("REPORT_SEND", "LT")!;
        var (subject, body) = ReportMailService.RenderTemplate(t, "LT", "UAB Test", "Skolų suderinimo aktas", "2026", "Bitės", "+370", "a@b.lt");
        Assert.Equal("Skolų suderinimo aktas 2026", subject);
        Assert.Contains("Gerbiamieji,", body);
        Assert.Contains("„Skolų suderinimo aktas“ (2026)", body);
        Assert.Contains("Pagarbiai,", body);
        Assert.DoesNotContain("{{", body);
    }

    [Fact]
    public void ReportSend_default_renders_in_EN_and_encodes_partner_name()
    {
        var t = EmailTemplateDefaults.Find("REPORT_SEND", "EN")!;
        var (_, body) = ReportMailService.RenderTemplate(t, "EN", "A <b>&</b> Co", "Sales", "2026-01-01 – 2026-03-31", null, null, null);
        Assert.Contains("A &lt;b&gt;&amp;&lt;/b&gt; Co", body);
        Assert.Contains("Kind regards,", body);
    }

    [Fact]
    public void Missing_partner_falls_back_to_generic_salutation()
    {
        var lt = ReportMailService.RenderTemplate(EmailTemplateDefaults.Find("REPORT_SEND", "LT")!, "LT", "", "R", "", null, null, null);
        Assert.Contains("Gerbiamieji,", lt.BodyHtml);
        var en = ReportMailService.RenderTemplate(EmailTemplateDefaults.Find("REPORT_SEND", "EN")!, "EN", " ", "R", "", null, null, null);
        Assert.Contains("Dear customer,", en.BodyHtml);
    }

    [Theory]
    [InlineData("a@x.lt;b@x.lt", "a@x.lt, b@x.lt")]
    [InlineData("  ", null)]
    public void Recipient_rule_is_shared_with_dunning(string raw, string? expected) =>
        Assert.Equal(expected, DunningEngine.ResolveRecipient(null, raw));

    [Fact]
    public async Task Send_without_selected_attachment_is_rejected()
    {
        var svc = new ReportMailService(null!, null!, null!, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SendAsync("a@x.lt", null, "s", "b", Array.Empty<EmailAttachmentSpec>(), "report", null));
    }
}
