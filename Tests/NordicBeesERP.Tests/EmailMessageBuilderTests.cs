using System.Text;
using MimeKit;
using NordicBeesERP.Models.Email;
using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Tests;

public class EmailMessageBuilderTests
{
    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 30)]
    [InlineData(3, 120)]
    [InlineData(4, 360)]
    public void Backoff_is_5m_30m_2h_6h(int attemptsMade, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), EmailMessageBuilder.NextAttemptDelay(attemptsMade));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Fifth_failed_attempt_means_permanent_failure(int attemptsMade)
    {
        Assert.Null(EmailMessageBuilder.NextAttemptDelay(attemptsMade));
    }

    [Fact]
    public void Build_sets_headers_body_attachment_and_message_id()
    {
        var options = new EmailOptions { FromAddress = "erp@example.test", FromName = "ERP", ReplyTo = "default-reply@example.test" };
        var row = new EmailOutbox { ToAddresses = "a@example.test, b@example.test", Subject = "S", BodyHtml = "<p>hi</p>", BodyText = "hi" };
        var delivery = new EmailDelivery(EmailMode.Live, "a@example.test, b@example.test", "c@example.test", "S", null);
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("pdf"));

        var msg = EmailMessageBuilder.Build(options, row, delivery, new[] { new EmailAttachmentContent("x.pdf", "application/pdf", content) });

        Assert.Equal("erp@example.test", ((MailboxAddress)msg.From[0]).Address);
        Assert.Equal(2, msg.To.Count);
        Assert.Single(msg.Cc);
        Assert.Equal("default-reply@example.test", ((MailboxAddress)msg.ReplyTo[0]).Address);
        Assert.Equal("S", msg.Subject);
        Assert.EndsWith("@example.test", msg.MessageId);
        Assert.Equal("<p>hi</p>", msg.HtmlBody);
        Assert.Equal("hi", msg.TextBody);
        Assert.Single(msg.Attachments);
    }

    [Fact]
    public void Row_reply_to_wins_over_option_default()
    {
        var options = new EmailOptions { FromAddress = "erp@example.test", ReplyTo = "default@example.test" };
        var row = new EmailOutbox { ToAddresses = "a@example.test", Subject = "S", BodyHtml = "x", ReplyTo = "row@example.test" };
        var delivery = new EmailDelivery(EmailMode.Live, "a@example.test", null, "S", null);

        var msg = EmailMessageBuilder.Build(options, row, delivery, Array.Empty<EmailAttachmentContent>());

        Assert.Equal("row@example.test", ((MailboxAddress)msg.ReplyTo[0]).Address);
    }

    [Fact]
    public void Redirect_mode_drops_row_reply_to_but_keeps_company_default()
    {
        var options = new EmailOptions { FromAddress = "erp@example.test", ReplyTo = "default@example.test" };
        var row = new EmailOutbox { ToAddresses = "client@real.test", Subject = "S", BodyHtml = "x", ReplyTo = "customer@real.test" };
        var delivery = new EmailDelivery(EmailMode.Redirect, "inbox@example.test", null, "[Staging → client@real.test] S", "client@real.test");

        var msg = EmailMessageBuilder.Build(options, row, delivery, Array.Empty<EmailAttachmentContent>());

        Assert.Equal("default@example.test", ((MailboxAddress)msg.ReplyTo[0]).Address);
    }
}
