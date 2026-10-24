using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// 0 % VAT legal-basis wording and the safe default VAT rate for new customers.
/// OWNER DECISIONS, PENDING ACCOUNTANT CONFIRMATION (see section "Buhalterei patikrinti" in .opencode/reports/VAT-02.md):
/// every text below and the default-rate matrix are named constants/one method so they can be changed in one place.
/// </summary>
public static class ZeroVatBasis
{
    public const string LtIntraCommunityNote = "PVM netaikomas pagal LR PVM įstatymo 49 str. (prekių tiekimas kitai ES valstybei narei).";
    public const string LtExportNote = "PVM netaikomas pagal LR PVM įstatymo 41 str. (prekių eksportas).";
    public const string EnIntraCommunityNote = "VAT exempt – intra-Community supply, Art. 138 Directive 2006/112/EC (LT VAT Law Art. 49).";
    public const string EnExportNote = "VAT exempt – export of goods (LT VAT Law Art. 41).";
    public const string ZeroVatToLtCustomerWarning = "Dėmesio: 0 % PVM Lietuvos klientui – patikrinkite PVM tarifą.";

    public const decimal StandardRate = 21m;

    private static readonly HashSet<string> EuCountryCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "EL", "HU", "IE", "IT",
        "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE"
    };

    public static bool IsEu(string? countryCode) =>
        !string.IsNullOrWhiteSpace(countryCode) && EuCountryCodes.Contains(countryCode.Trim());

    /// <summary>Upper-case ISO country; falls back to the two-letter VAT-code prefix when the country is blank. Null = unknown.</summary>
    public static string? ResolveCountry(string? countryCode, string? vatCode)
    {
        var cc = countryCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(cc)) return cc == "EL" ? "GR" : cc;
        var v = vatCode?.Trim().ToUpperInvariant();
        if (v is { Length: >= 3 } && char.IsLetter(v[0]) && char.IsLetter(v[1]))
            return v[..2] == "EL" ? "GR" : v[..2];
        return null;
    }

    /// <summary>Legal-basis note for a standard invoice with a 0 % line; null when none applies (LT/unknown customer, no 0 % line, RC96/ULAK).</summary>
    public static string? NoteFor(string? customerCountryCode, string? customerVatCode, IEnumerable<decimal> lineVatRates, bool english)
    {
        if (!lineVatRates.Any(r => r == 0m)) return null;
        var country = ResolveCountry(customerCountryCode, customerVatCode);
        if (country is null || country == "LT") return null;
        return IsEu(country)
            ? (english ? EnIntraCommunityNote : LtIntraCommunityNote)
            : (english ? EnExportNote : LtExportNote);
    }

    /// <summary>Default VAT rate for a NEW customer: never 0 % for LT or unknown country.</summary>
    public static decimal DefaultRateForNewCustomer(string? countryCode, string? vatCode)
    {
        var country = ResolveCountryStrict(countryCode);
        if (country is null || country == "LT") return StandardRate;
        if (IsEu(country)) return string.IsNullOrWhiteSpace(vatCode) ? StandardRate : 0m;
        return 0m; // outside the EU (export)
    }

    private static string? ResolveCountryStrict(string? countryCode)
    {
        var cc = countryCode?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(cc) ? null : (cc == "EL" ? "GR" : cc);
    }

    public static bool ShouldWarnZeroVatToLtCustomer(string? customerCountryCode, string? customerVatCode, IEnumerable<decimal> lineVatRates) =>
        ResolveCountry(customerCountryCode, customerVatCode) == "LT" && lineVatRates.Any(r => r == 0m);
}
