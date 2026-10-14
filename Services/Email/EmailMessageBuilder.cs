using MimeKit;
using MimeKit.Utils;
using NordicBeesERP.Models.Email;

namespace NordicBeesERP.Services.Email;

/// <summary>Attachment ready to be added to a MimeMessage (stream owned by the caller).</summary>
public sealed record EmailAttachmentContent(string FileName, string ContentType, Stream Content);

/// <summary>Pure helpers for the outbox worker: MIME construction and retry backoff. No I/O.</summary>
public static class EmailMessageBuilder
{
    /// <summary>Total send attempts before a row becomes 'failed'.</summary>
    public const int MaxAttempts = 5;

    private static readonly TimeSpan[] Backoff =
    {
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(6)
    };

    /// <summary>
    /// Delay before the next attempt after <paramref name="attemptsMade"/> failed attempts (1-based:
    /// 1 → 5 min, 2 → 30 min, 3 → 2 h, 4 → 6 h). Null once <see cref="MaxAttempts"/> is reached — the row fails.
    /// </summary>
    public static TimeSpan? NextAttemptDelay(int attemptsMade)
    {
        if (attemptsMade >= MaxAttempts)
        {
            return null;
        }

        return Backoff[Math.Clamp(attemptsMade, 1, Backoff.Length) - 1];
    }

    public static MimeMessage Build(
        EmailOptions options,
        EmailOutbox row,
        EmailDelivery delivery,
        IReadOnlyList<EmailAttachmentContent> attachments)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(options.FromName ?? string.Empty, options.FromAddress));
        message.To.AddRange(InternetAddressList.Parse(delivery.To));
        if (!string.IsNullOrWhiteSpace(delivery.Cc))
        {
            message.Cc.AddRange(InternetAddressList.Parse(delivery.Cc));
        }

        var replyTo = !string.IsNullOrWhiteSpace(row.ReplyTo) ? row.ReplyTo : options.ReplyTo;
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyTo.Add(MailboxAddress.Parse(replyTo));
        }

        message.Subject = delivery.Subject;
        var domain = options.FromAddress?.Split('@').LastOrDefault();
        message.MessageId = string.IsNullOrWhiteSpace(domain) ? MimeUtils.GenerateMessageId() : MimeUtils.GenerateMessageId(domain);

        var body = new BodyBuilder { HtmlBody = row.BodyHtml, TextBody = row.BodyText };
        foreach (var a in attachments)
        {
            body.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        }

        message.Body = body.ToMessageBody();
        return message;
    }
}
