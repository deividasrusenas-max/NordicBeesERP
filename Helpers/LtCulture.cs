using System.Globalization;

namespace NordicBeesERP.Helpers;

/// <summary>
/// One numeric-input convention for the whole UI. MudNumericField without a Culture parses with
/// InvariantCulture, so a lt-LT user typing "308,80" gets 30880. Every decimal MudNumericField passes
/// <see cref="Instance"/> as Culture; manual text parsing goes through <see cref="TryParseDecimal"/>.
/// </summary>
public static class LtCulture
{
    public static readonly CultureInfo Instance = Create();

    private static CultureInfo Create()
    {
        try
        {
            // ICU lt-LT writes the negative sign as U+2212; keep ASCII "-" so formatted values re-parse and copy/paste cleanly
            var lt = new CultureInfo("lt-LT");
            lt.NumberFormat.NegativeSign = "-";
            return lt;
        }
        catch (CultureNotFoundException) { }

        // No ICU data (invariant-globalization container): same decimal comma, no thousands grouping.
        var fallback = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        fallback.NumberFormat.NumberDecimalSeparator = ",";
        fallback.NumberFormat.NumberGroupSeparator = " ";
        fallback.NumberFormat.CurrencyDecimalSeparator = ",";
        fallback.NumberFormat.CurrencyGroupSeparator = " ";
        return fallback;
    }

    /// <summary>Kept for existing callers; see <see cref="LtNumber.TryParseDecimal"/>.</summary>
    public static bool TryParseDecimal(string? text, out decimal value) => LtNumber.TryParseDecimal(text, out value);
}
