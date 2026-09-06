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
    /// Returns true when a fresh VIES lookup should be performed.
    /// - Never for individuals (IsIndividual) or when the VAT code is empty.
    /// - Always when VatVerified is null (never checked before).
    /// - Also when the VAT code differs from the persisted one
    ///   (changed since last save/load).
    /// - Otherwise false (already verified, code unchanged — do NOT re-check).
    /// </summary>
    public static bool ShouldVerifyVies(bool isIndividual, string? vatCode, string? lastCheckedVatCode, bool? vatVerified)
    {
        if (isIndividual) return false;
        if (string.IsNullOrWhiteSpace(vatCode)) return false;
        if (vatVerified == null) return true;
        if (string.Equals(vatCode.Trim(), lastCheckedVatCode?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}
