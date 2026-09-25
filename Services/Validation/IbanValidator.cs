using System.Numerics;

namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Why an IBAN failed validation. Pure, no I/O — OCR Etapas 1 prep, not yet wired in.
/// </summary>
public enum IbanValidationReason
{
    Empty,
    BadCharacters,
    UnknownCountryLength,
    WrongLength,
    ChecksumFailed
}

/// <summary>
/// Result of <see cref="IbanValidator.Validate"/>. <see cref="IsValid"/> is decided by the
/// ISO 13616 mod-97 checksum; <see cref="Reason"/> explains an invalid result (null when valid);
/// <see cref="Warnings"/> carries non-fatal notes (e.g. country not in the known-length table).
/// </summary>
public sealed record IbanValidationResult
{
    public required bool IsValid { get; init; }

    /// <summary>Upper-case, whitespace-stripped IBAN as given (null when input was empty).</summary>
    public string? NormalizedIban { get; init; }

    /// <summary>First two characters of the normalized IBAN, when available.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Failure reason; null when valid.</summary>
    public IbanValidationReason? Reason { get; init; }

    /// <summary>Non-fatal notes (e.g. <see cref="IbanValidationReason.UnknownCountryLength"/>).</summary>
    public IReadOnlyList<IbanValidationReason> Warnings { get; init; } = Array.Empty<IbanValidationReason>();
}

/// <summary>
/// Validates an IBAN: normalisation, per-country length check (ISO 13616 registry) and the
/// ISO 13616 mod-97 checksum. Pure static, no I/O — OCR Etapas 1 prep, not yet wired in.
/// </summary>
public static class IbanValidator
{
    // Known IBAN lengths per country. Source: ISO 13616 registry (IBAN country list),
    // subset relevant to this project's suppliers/customers. Countries missing from this
    // table are still validated by mod-97, with an UnknownCountryLength warning attached.
    private static readonly Dictionary<string, int> KnownCountryLengths = new()
    {
        ["LT"] = 20, ["LV"] = 21, ["EE"] = 20, ["DE"] = 22, ["PL"] = 28,
        ["RO"] = 24, ["FI"] = 18, ["SE"] = 24, ["NL"] = 18, ["BE"] = 16,
        ["DK"] = 18, ["AT"] = 20, ["CZ"] = 24, ["SK"] = 24, ["FR"] = 27,
        ["IT"] = 27, ["ES"] = 24, ["GB"] = 22, ["UA"] = 29
    };

    /// <summary>
    /// Validates a raw IBAN string. Never throws for any string input. Null/whitespace input is
    /// Empty; whitespace is stripped and the result upper-cased; characters outside A-Z0-9 are
    /// BadCharacters (checked before length); fewer than 5 characters is WrongLength (no room for
    /// country + check digits + BBAN); a non-letter in the two country positions is BadCharacters;
    /// known countries must match their exact registered length, unknown countries only need
    /// 15..34 (with an UnknownCountryLength warning); finally the ISO 13616 mod-97 checksum must
    /// equal 1.
    /// </summary>
    public static IbanValidationResult Validate(string? raw)
    {
        if (raw is null || raw.Trim().Length == 0)
            return new IbanValidationResult { IsValid = false, Reason = IbanValidationReason.Empty };

        var normalized = new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var countryCode = normalized.Length >= 2 ? normalized[..2] : null;

        if (normalized.Any(c => (c < '0' || c > '9') && (c < 'A' || c > 'Z')))
            return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.BadCharacters };

        if (normalized.Length < 5)
            return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.WrongLength };

        if (!IsAsciiLetter(normalized[0]) || !IsAsciiLetter(normalized[1]))
            return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.BadCharacters };

        var warnings = new List<IbanValidationReason>();
        if (KnownCountryLengths.TryGetValue(countryCode!, out var expectedLength))
        {
            if (normalized.Length != expectedLength)
                return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.WrongLength };
        }
        else
        {
            if (normalized.Length < 15 || normalized.Length > 34)
                return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.WrongLength };

            warnings.Add(IbanValidationReason.UnknownCountryLength);
        }

        if (!HasValidMod97(normalized))
            return new IbanValidationResult { IsValid = false, NormalizedIban = normalized, CountryCode = countryCode, Reason = IbanValidationReason.ChecksumFailed, Warnings = warnings };

        return new IbanValidationResult { IsValid = true, NormalizedIban = normalized, CountryCode = countryCode, Warnings = warnings };
    }

    // ISO 13616: move the first 4 characters to the end, map A=10..Z=35, mod 97 must be 1.
    // BigInteger because a 34-character IBAN expands to up to 102 digits — overflows long.
    private static bool HasValidMod97(string normalized)
    {
        var rearranged = normalized[4..] + normalized[..4];

        var value = BigInteger.Zero;
        foreach (var c in rearranged)
            value = char.IsDigit(c) ? value * 10 + (c - '0') : value * 100 + (c - 'A' + 10);

        return value % 97 == 1;
    }

    private static bool IsAsciiLetter(char c) => c >= 'A' && c <= 'Z';
}
