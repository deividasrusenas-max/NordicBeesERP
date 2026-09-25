namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Why a VAT code failed format validation. Pure, no I/O — OCR Etapas 1 prep, not yet wired in.
/// </summary>
public enum VatCodeValidationReason
{
    Empty,
    UnknownCountry,
    WrongFormat
}

/// <summary>
/// Result of <see cref="VatCodeFormatValidator.Validate"/>. <see cref="Reason"/> is null when valid.
/// </summary>
public sealed record VatCodeValidationResult
{
    public required bool IsValid { get; init; }

    /// <summary>
    /// Upper-case code with spaces, dots and dashes removed, always carrying the country prefix
    /// (the hint is prepended when the input had none). Null when input was empty.
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
    /// Validates a raw VAT code. Null/whitespace is Empty. Spaces, dots and dashes are removed and
    /// the rest upper-cased. A leading 2-letter prefix selects the country (it wins over
    /// <paramref name="countryHint"/>); without a prefix the code is accepted only when
    /// <paramref name="countryHint"/> names a supported country. An unsupported or missing country
    /// is UnknownCountry; a body that is not the allowed number of digits is WrongFormat.
    /// </summary>
    public static VatCodeValidationResult Validate(string? raw, string? countryHint = null)
    {
        if (raw is null || raw.Trim().Length == 0)
            return new VatCodeValidationResult { IsValid = false, Reason = VatCodeValidationReason.Empty };

        var normalized = new string(raw.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray())
            .ToUpperInvariant();

        string country;
        string body;
        if (normalized.Length >= 2 && IsAsciiLetter(normalized[0]) && IsAsciiLetter(normalized[1]))
        {
            country = normalized[..2];
            body = normalized[2..];
        }
        else
        {
            var hint = countryHint?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(hint))
                return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, Reason = VatCodeValidationReason.UnknownCountry };

            country = hint;
            body = normalized;
            normalized = hint + normalized;
        }

        if (!AllowedDigitCounts.TryGetValue(country, out var digitCounts))
            return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, CountryCode = country, Reason = VatCodeValidationReason.UnknownCountry };

        if (!digitCounts.Contains(body.Length) || !body.All(c => c >= '0' && c <= '9'))
            return new VatCodeValidationResult { IsValid = false, NormalizedCode = normalized, CountryCode = country, Reason = VatCodeValidationReason.WrongFormat };

        return new VatCodeValidationResult { IsValid = true, NormalizedCode = normalized, CountryCode = country };
    }

    private static bool IsAsciiLetter(char c) => c >= 'A' && c <= 'Z';
}
