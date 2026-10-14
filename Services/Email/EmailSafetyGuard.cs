namespace NordicBeesERP.Services.Email;

/// <summary>Outcome of the safety decision for one message.</summary>
public sealed record EmailDelivery(EmailMode EffectiveMode, string To, string? Cc, string Subject, string? OriginalTo);

/// <summary>
/// Pure decision logic that keeps non-production environments (and misconfigured production) from
/// mailing real customers. No I/O, no DI — unit-tested over every environment × mode × RedirectTo combination.
/// </summary>
public static class EmailSafetyGuard
{
    private const int OriginalToMaxLength = 1000;

    /// <summary>
    /// Live only when the environment is Production AND Mode == Live. Otherwise Redirect when RedirectTo is set,
    /// else Disabled. A configured Mode of Disabled is never upgraded: it always stays Disabled.
    /// </summary>
    public static EmailMode ResolveEffectiveMode(string environmentName, EmailOptions options)
    {
        if (options.Mode == EmailMode.Disabled)
        {
            return EmailMode.Disabled;
        }

        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        if (isProduction && options.Mode == EmailMode.Live)
        {
            return EmailMode.Live;
        }

        return string.IsNullOrWhiteSpace(options.RedirectTo) ? EmailMode.Disabled : EmailMode.Redirect;
    }

    /// <summary>
    /// Applies the effective mode to a message. Redirect replaces To and Cc with RedirectTo, keeps the originals in
    /// <see cref="EmailDelivery.OriginalTo"/> and prefixes the subject with "[ENV → original to] ".
    /// Live passes everything through unchanged; Disabled returns the message untouched (it is never sent).
    /// </summary>
    public static EmailDelivery Apply(string environmentName, EmailOptions options, string to, string? cc, string subject)
    {
        var mode = ResolveEffectiveMode(environmentName, options);
        if (mode != EmailMode.Redirect)
        {
            return new EmailDelivery(mode, to, cc, subject, null);
        }

        var original = string.IsNullOrWhiteSpace(cc) ? to : $"{to}; cc: {cc}";
        if (original.Length > OriginalToMaxLength)
        {
            original = original[..OriginalToMaxLength];
        }

        return new EmailDelivery(
            EmailMode.Redirect,
            options.RedirectTo!.Trim(),
            null,
            $"[{environmentName} → {to}] {subject}",
            original);
    }
}
