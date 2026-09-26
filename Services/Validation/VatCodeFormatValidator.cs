namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Why a VAT code failed format validation. Pure, no I/O — OCR Etapas 1 prep, not yet wired in.
/// </summary>
public enum VatCodeValidationReason
{
    Empty,
    UnknownCountry,
    WrongFormat,

    /// <summary>
    /// The code is well-formed for its own prefix, but the prefix differs from the country hint
    /// (e.g. the supplier's address country). Not a format error — a company may be VAT-registered
    /// in another member state — so it is reported separately and the caller decides what it means.
    /// </summary>
    CountryMismatch
}

/// <summary>
/// Result of <see cref="VatCodeFormatValidator.Validate"/>. <see cref="Reason"/> is null when valid.
/// </summary>
public sealed record VatCodeValidationResult
{
    public required bool IsValid { get; init; }

    /// <summary>
    /// Upper-case code with whitespace, dots and dash characters removed, carrying the country prefix
    /// (a valid hint is prepended when the input had none). Null when the input was empty.
    /// </summary>
    public string? NormalizedCode { get; init; }

    /// <summary>Two-letter VAT prefix the code was checked against, when determined.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Failure reason; null when valid.</summary>
    public VatCodeValidationReason? Reason { get; init; }
}

/// <summary>
/// Checks the FORMAT of an EU VAT code (prefix + digit count) for the countries this project's
/// suppliers come from. Pure static, no I/O — OCR Etapas 1 prep, not yet wired in.
/// <para>
/// Format only — no check-digit validation. The LT check-digit algorithm is unknown (open
/// question Q-005) and must not be invented; the other countries' checksums are likewise not
/// implemented. A format-valid code may still not exist — VIES is the authority for that.
/// </para>
/// </summary>
public static class VatCodeFormatValidator
{
    // Allowed digit counts after the 2-letter prefix (source: task spec
    // .opencode/tasks/ocr-etapas1-validators.md, commit 2; LT/LV/EE/DE/RO also in
    // Docs/ocr-rebuild/analysis/RESEARCH-2026-09-25-reliability.md §3).
    // Greece (EL, not GR) is deliberately not included.
    private static readonly Dictionary<string, int[]> AllowedDigitCounts = new()
    {
        ["LT"] = new[] { 9, 12 },
        ["LV"] = new[] { 11 },
        ["EE"] = new[] { 9 },
        ["DE"] = new[] { 9 },
        ["PL"] = new[] { 10 },
        ["RO"] = Enumerable.Range(2, 9).ToArray() // 2..10
    };

    /// <summary>
    /// Validates a raw VAT code. Null, whitespace or separator-only input is Empty. Whitespace, dots
    /// and dash characters (hyphen, en/em dash, minus sign and other Unicode dash punctuation) are
    /// removed and the rest upper-cased. A leading 2-letter prefix selects the country; without a
    /// prefix the code is accepted only when <paramref name="countryHint"/> is exactly two ASCII
    /// letters naming a supported country (any other hint counts as no hint). An unsupported or
    /// missing country is UnknownCountry; a body that is not the allowed number of digits is
    /// WrongFormat; a well-formed prefixed code whose prefix differs from a valid hint is
    /// CountryMismatch (checked last, so a malformed code is always WrongFormat).
    /// </summary>
    public static VatCodeValidationResult Validate(string? raw, string? countryHint = null)
    {
        if (raw is null)
            return new VatCodeValidationResult { IsValid = false, Reason = VatCodeValidationReason.Empty };

        var normalized = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '.' && !IsDash(c)).ToArray())
            .ToUpperInvariant();
        if (normalized.Length == 0)
            return new VatCodeValidationResult { IsValid = false, Reason = VatCodeValidationReason.Empty };

        var hint = NormalizeHint(countryHint);

        string country;
        string body;
        bool prefixed;
        if (normalized.Length >= 2 && IsAsciiLetter(normalized[0]) && IsAsciiLetter(normalized[1]))
        {
            country = normalized[..2];
            body = normalized[2..];
            prefixed = true;
        }
        else
        {
            if (hint is null)
                return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, Reason = VatCodeValidationReason.UnknownCountry };

            country = hint;
            body = normalized;
            normalized = hint + normalized;
            prefixed = false;
        }

        if (!AllowedDigitCounts.TryGetValue(country, out var digitCounts))
            return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, CountryCode = country, Reason = VatCodeValidationReason.UnknownCountry };

        if (!digitCounts.Contains(body.Length) || !body.All(c => c >= '0' && c <= '9'))
            return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, CountryCode = country, Reason = VatCodeValidationReason.WrongFormat };

        if (prefixed && hint is not null && hint != country)
            return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, CountryCode = country, Reason = VatCodeValidationReason.CountryMismatch };

        return new VatCodeValidationResult { IsValid = true, NormalizedCode = normalized, CountryCode = country };
    }

    // A usable hint is exactly two ASCII letters after trimming; anything else ("LTU", "L", "1T") is ignored.
    private static string? NormalizeHint(string? countryHint)
    {
        var hint = countryHint?.Trim().ToUpperInvariant();
        return hint is { Length: 2 } && IsAsciiLetter(hint[0]) && IsAsciiLetter(hint[1]) ? hint : null;
    }

    // Hyphen-minus, Unicode dash punctuation (U+2010–U+2015, U+2E3A, …) and the minus sign U+2212,
    // which OCR produces in place of a hyphen.
    private static bool IsDash(char c) =>
        c == '-' || c == '\u2212' || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.DashPunctuation;

    private static bool IsAsciiLetter(char c) => c >= 'A' && c <= 'Z';
}
