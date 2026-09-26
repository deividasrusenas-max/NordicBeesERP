using System.Text.RegularExpressions;

namespace NordicBeesERP.Services.Validation;

public enum CompanyCodeSource
{
    None,
    /// <summary>Azure's <c>VendorBusinessNumber</c> field, when it holds a plausible registration number.</summary>
    BusinessNumberField,
    /// <summary>A number right after a registration-number label in the document text („Įmonės kodas", „Reg. Nr.", "Company code", …).</summary>
    LabelledText,
    /// <summary>Two or more different candidates remain after the own company's codes are excluded — nothing is returned (loud, D-017).</summary>
    Ambiguous
}

public sealed record CompanyCodeExtraction(string Code, CompanyCodeSource Source);

/// <summary>
/// The supplier's registration ("company") code from an Azure response (PLAN-ETAPAS2 §0.3, D-045). The old code filled
/// <c>SupplierCompanyCode</c> with the whole VAT code (prefix included) or with a name; this returns <b>a registration number or
/// an empty string</b> — never a VAT code, never a name. A registration number is 7–14 digits; a VAT code is never derived
/// from it or it from a VAT code (D-045, the Artea invoice). Pure, no I/O.
/// <para>
/// Sources, in order: (1) <c>VendorBusinessNumber</c>; (2) a number after a registration label in the text. Every candidate is
/// dropped when it equals one of the <paramref name="excludedCodes"/> — the buyer's codes (our own company code and the digits
/// of our own / the buyer's VAT code), because an invoice prints both parties' codes. If exactly one distinct candidate is
/// left it is the answer; if several are left the answer is empty (a guess would be silent).
/// </para>
/// <para>
/// Deliberately not a source: "NIP" (Poland's tax id, the VAT tier's job, not a registration number); the vendor address
/// recipient (a name — 0 of 9 recipients in the Etapas 2 corpus held a number); a bare "Kodas" is accepted only with exactly
/// 9 digits and never after PVM / mokėtojo / banko / … (bank codes have 5 digits).
/// </para>
/// </summary>
public static class SupplierCompanyCodeExtractor
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private const string Labels =
        @"(?:[ĮI]m(?:onės)?\.?\s*kod(?:as)?" +
        @"|[ĮI]\.\s*k\.(?:\s*/\s*Reg\.?\s*no\.?)?" +
        @"|Company\s+(?:registration\s+)?(?:code|number|no\.?)" +
        @"|Registration\s+(?:code|number|no\.?)" +
        @"|Reg(?:istry)?\.?\s*(?:Nr|no|number|code)\.?" +
        @"|Registrikood" +
        @"|Reģ\.?\s*nr\.?" +
        @"|KRS|REGON|Handelsregisternummer)";

    private static readonly Regex Labelled = new(
        Labels + @"\s*[:\-–]?\s*(?<v>\d{7,14})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex BareKodas = new(
        @"\bkodas\b\s*[:\-–]?\s*(?<v>\d{9})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex NotACompanyCodeBefore = new(
        @"(pvm|mokėtojo|banko|bank|swift|bic|kliento|asmens|sąsk\w*|pašto|poštinis)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex PlainNumber = new(@"^\s*\d[\d\s.\-]*\d\s*$", RegexOptions.CultureInvariant, Timeout);

    public static CompanyCodeExtraction Extract(string? businessNumber, string? content, IEnumerable<string?> excludedCodes)
    {
        var excluded = new HashSet<string>(excludedCodes.Select(Digits).Where(d => d.Length > 0));

        // the field counts only when it is a plain number: "J40/1234/2005" or "LT123456789" are not
        var fromField = businessNumber != null && PlainNumber.IsMatch(businessNumber) ? Digits(businessNumber) : string.Empty;
        if (IsPlausible(fromField) && !excluded.Contains(fromField))
            return new CompanyCodeExtraction(fromField, CompanyCodeSource.BusinessNumberField);

        var candidates = new HashSet<string>();
        if (!string.IsNullOrEmpty(content))
        {
            foreach (Match m in Labelled.Matches(content))
                candidates.Add(m.Groups["v"].Value);

            foreach (Match m in BareKodas.Matches(content))
            {
                var before = content[Math.Max(0, m.Index - 14)..m.Index];
                if (!NotACompanyCodeBefore.IsMatch(before))
                    candidates.Add(m.Groups["v"].Value);
            }
        }

        candidates.RemoveWhere(c => excluded.Contains(c));

        return candidates.Count switch
        {
            1 => new CompanyCodeExtraction(candidates.First(), CompanyCodeSource.LabelledText),
            > 1 => new CompanyCodeExtraction(string.Empty, CompanyCodeSource.Ambiguous),
            _ => new CompanyCodeExtraction(string.Empty, CompanyCodeSource.None)
        };
    }

    private static bool IsPlausible(string digits) => digits.Length is >= 7 and <= 14;

    private static string Digits(string? raw) => raw == null ? string.Empty : new string(raw.Where(char.IsAsciiDigit).ToArray());
}
