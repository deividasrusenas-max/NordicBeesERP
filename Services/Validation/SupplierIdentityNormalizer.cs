using System.Text;
using System.Text.RegularExpressions;
using NordicBeesERP.Helpers;

namespace NordicBeesERP.Services.Validation;

/// <summary>A VAT code as the matcher compares it: upper-case, separators removed, the country prefix kept when there is one.</summary>
/// <param name="Normalized">Null when the input was empty.</param>
/// <param name="HasPrefix">True when the normalised code starts with a two-letter country prefix (given or added from the hint).</param>
public sealed record VatIdentity(string? Normalized, bool HasPrefix);

/// <summary>
/// Normalisation of the four supplier identifiers the matcher compares (PLAN-ETAPAS2 §1.2, D-044, D-045).
/// Pure, no I/O. Two hard rules: an empty or unusable input normalises to null / "" and callers must never
/// match on it (the partner-336 case, D-017), and no identifier is ever derived from another — a VAT code is
/// not "LT" + a company code (D-045, the Artea invoice).
/// </summary>
public static class SupplierIdentityNormalizer
{
    // ------------------------------------------------------------------ VAT

    /// <summary>
    /// VAT code, whitespace / dots / dashes removed (<see cref="VatCodeFormatValidator"/>'s normalisation), upper-case,
    /// prefix kept. A code with no prefix gets the prefix of <paramref name="countryHint"/> when that is a valid ISO
    /// country (GR → EL); with no usable hint it stays prefix-less — there is <b>no "LT" assumption</b> (D-045 Q8),
    /// so a prefix-less code only equals another prefix-less code with the same characters.
    /// </summary>
    public static VatIdentity Vat(string? raw, string? countryHint = null)
    {
        var normalized = VatCodeFormatValidator.Validate(raw).NormalizedCode;
        if (string.IsNullOrEmpty(normalized)) return new VatIdentity(null, false);

        if (normalized.Length >= 2 && IsAsciiLetter(normalized[0]) && IsAsciiLetter(normalized[1]))
            return new VatIdentity(normalized, true);

        if (CountryCodeResolver.IsValidIso(countryHint))
        {
            var country = countryHint!.Trim().ToUpperInvariant();
            var prefix = country == "GR" ? "EL" : country;
            return new VatIdentity(prefix + normalized, true);
        }

        return new VatIdentity(normalized, false);
    }

    // ------------------------------------------------------------------ company (registration) code

    /// <summary>
    /// Company registration code: letters and digits only, upper-case. Null when empty, when it has no digit, or when it
    /// looks like a prefixed VAT code (two letters + digits) — a VAT code is not a registration number and is never
    /// accepted as one (PLAN-ETAPAS2 §0.3). No country prefix is stripped or added.
    /// </summary>
    public static string? CompanyCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var code = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (code.Length == 0 || !code.Any(char.IsDigit)) return null;
        if (Regex.IsMatch(code, "^[A-Z]{2}[0-9]{2,}$")) return null;
        return code;
    }

    // ------------------------------------------------------------------ IBAN

    /// <summary>The upper-case, whitespace-free IBAN when it passes <see cref="IbanValidator"/> (length + mod-97); otherwise null — an invalid IBAN is never a key.</summary>
    public static string? Iban(string? raw)
    {
        var result = IbanValidator.Validate(raw);
        return result.IsValid ? result.NormalizedIban : null;
    }

    // ------------------------------------------------------------------ name

    /// <summary>
    /// "Exact" name key: trimmed, whitespace collapsed, case and diacritics folded (<see cref="DiacriticHelper.Fold"/>) —
    /// what the <c>utf8mb4_unicode_ci</c> collation already treats as equal, so today's exact match keeps its meaning
    /// (PLAN-ETAPAS2 §0.2 item 5). Punctuation and legal forms are kept. Empty string when there is no name.
    /// </summary>
    public static string NameExact(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        return CollapseSpaces(DiacriticHelper.Fold(raw));
    }

    // "and" in the languages of our suppliers (folded); "&" is handled before punctuation is dropped
    private static readonly HashSet<string> AndTokens = new() { "ir", "und", "si", "and" };

    // Legal-form tokens after folding (Į→i, Š→s, Ž→z, Ū→u, Ü→u) and punctuation → space, longest sequence first.
    private static readonly string[][] LegalForms =
    {
        new[] { "sp", "z", "o", "o" }, new[] { "z", "o", "o" }, new[] { "s", "r", "l" }, new[] { "s", "p", "a" },
        new[] { "a", "s" }, new[] { "s", "a" }, new[] { "o", "u" },
        new[] { "uab" }, new[] { "ab" }, new[] { "mb" }, new[] { "ii" }, new[] { "vsi" }, new[] { "zub" }, new[] { "ub" },
        new[] { "kb" }, new[] { "sia" }, new[] { "as" }, new[] { "ou" }, new[] { "gmbh" }, new[] { "ag" }, new[] { "ug" },
        new[] { "kg" }, new[] { "ohg" }, new[] { "srl" }, new[] { "sa" }, new[] { "sarl" }, new[] { "spa" }, new[] { "zoo" },
        new[] { "ltd" }, new[] { "llc" }, new[] { "bv" }, new[] { "nv" }, new[] { "oy" }, new[] { "aps" },
        new[] { "тов" }, new[] { "фоп" },
    };

    /// <summary>
    /// "Normalised" name key (PLAN-ETAPAS2 §1.2 tier 4b): <see cref="NameExact"/> plus quotes and punctuation dropped,
    /// <c>&amp; / ir / und / și / and</c> unified, and legal-form tokens (UAB, AB, MB, IĮ, VšĮ, ŽŪB, SIA, OÜ/AS, GmbH/AG/UG,
    /// SRL/SA, Sp. z o.o., …) stripped from the <b>ends</b> of the name only. Empty string when nothing is left — such a
    /// key must never be matched on. Names that differ only in legal form (UAB Rotoma / AB Rotoma) become equal here, which is
    /// why this key only ever <em>suggests</em> (D-044 Q1, Q3).
    /// </summary>
    public static string NameNormalized(string? raw)
    {
        var folded = NameExact(raw);
        if (folded.Length == 0) return string.Empty;

        var s = folded.Replace("&", " and ", StringComparison.Ordinal);

        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        var tokens = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => AndTokens.Contains(t) ? "and" : t)
            .ToList();

        var changed = true;
        while (changed && tokens.Count > 0)
        {
            changed = false;
            foreach (var form in LegalForms.OrderByDescending(f => f.Length))
            {
                if (form.Length <= tokens.Count && tokens.Take(form.Length).SequenceEqual(form))
                {
                    tokens.RemoveRange(0, form.Length);
                    changed = true;
                    break;
                }
                if (form.Length <= tokens.Count && tokens.Skip(tokens.Count - form.Length).SequenceEqual(form))
                {
                    tokens.RemoveRange(tokens.Count - form.Length, form.Length);
                    changed = true;
                    break;
                }
            }
        }

        // a key made only of conjunctions ("Ir Ir", "&") says nothing about the company — it must never match
        if (tokens.All(t => t == "and")) return string.Empty;

        return string.Join(' ', tokens);
    }

    // ------------------------------------------------------------------ helpers

    private static string CollapseSpaces(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z';
}
