using System.Text.RegularExpressions;
using NordicBeesERP.Models;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Decides whether a fresh VIES VAT-verification check is needed for a
/// business partner (customer or supplier) when their dialog opens or
/// their VAT code changes. Keeps the decision shared between both
/// dialogs so VIES is not hammered on every open/save.
/// </summary>
public static class VatVerificationHelper
{
    /// <summary>
    /// Matches a real EU VAT number format (2-letter country prefix, e.g.
    /// "LT100007970728"). LT ūkininkai, kuriems taikoma kompensacinio PVM
    /// tarifo schema, turi VMI suteiktą VIETINĮ 12-13 ženklų registracijos
    /// kodą BE šalies prefikso (pvz. "100008534429") — tai NĖRA PVM mokėtojo
    /// kodas ir VIES apie tokius kodus nieko nežino (visada gražintų
    /// "negalioja", nors kodas visiškai teisėtas). Todėl VIES kviečiamas TIK
    /// kai kodas turi šalies raidžių prefiksą.
    /// </summary>
    private static readonly Regex EuVatFormat = new("^[A-Za-z]{2}", RegexOptions.Compiled);

    /// <summary>
    /// Returns true when a fresh VIES lookup should be performed.
    /// - Never when the VAT code is empty.
    /// - Never when the code doesn't look like a real EU VAT number (no
    ///   2-letter country prefix) — e.g. LT ūkininkų kompensacinio PVM
    ///   tarifo registracijos kodas (be prefikso), kuris VIES sistemoje
    ///   neegzistuoja. NOTE: does NOT blanket-skip individuals — LT ūkininkas
    ///   gali būti ir realus PVM mokėtojas (21% ar 6% tarifas), tokiu atveju
    ///   jo kodas TURI šalies prefiksą ir yra VIES patikrinamas.
    /// - Always when VatVerified is null (never checked before).
    /// - Also when the VAT code differs from the persisted one
    ///   (changed since last save/load).
    /// - Otherwise false (already verified, code unchanged — do NOT re-check).
    /// </summary>
    public static bool ShouldVerifyVies(bool isIndividual, string? vatCode, string? lastCheckedVatCode, bool? vatVerified)
    {
        if (string.IsNullOrWhiteSpace(vatCode)) return false;
        if (!EuVatFormat.IsMatch(vatCode.Trim())) return false;
        if (vatVerified == null) return true;
        if (string.Equals(vatCode.Trim(), lastCheckedVatCode?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
