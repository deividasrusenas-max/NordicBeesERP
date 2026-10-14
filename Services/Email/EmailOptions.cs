namespace NordicBeesERP.Services.Email;

public enum EmailMode
{
    Disabled,
    Redirect,
    Live
}

/// <summary>
/// Section "Email". Values (including the SMTP/IMAP password) come ONLY from IConfiguration
/// (env vars such as Email__Password) — never from the app_settings table: the staging DB is a prod clone.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public EmailMode Mode { get; set; } = EmailMode.Disabled;
    public string? RedirectTo { get; set; }
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? ReplyTo { get; set; }
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 465;
    public bool SmtpSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ImapHost { get; set; }
    public int ImapPort { get; set; } = 993;
    public string SentFolder { get; set; } = "INBOX.Sent";
    public int MaxPerHour { get; set; } = 60;
}
