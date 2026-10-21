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
        try { return new CultureInfo("lt-LT"); }
        catch (CultureNotFoundException) { }

        // No ICU data (invariant-globalization container): same decimal comma, no thousands grouping.
        var fallback = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        fallback.NumberFormat.NumberDecimalSeparator = ",";
        fallback.NumberFormat.NumberGroupSeparator = " ";
        fallback.NumberFormat.CurrencyDecimalSeparator = ",";
        fallback.NumberFormat.CurrencyGroupSeparator = " ";
        return fallback;
    }

    /// <summary>
    /// Parses user-typed text: "308,80" -> 308.80, "1 234,5" -> 1234.5, "19.5" -> 19.5.
    /// A single separator (comma or dot) is the decimal separator; spaces / NBSP are grouping and ignored.
    /// Ambiguous input with several separators ("1.234,5", "1,234.5") is rejected rather than guessed.
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var cleaned = text.Trim()
            .Replace(" ", "").Replace(" ", "").Replace(" ", "")
            .Replace(',', '.');

        if (cleaned.Count(c => c == '.') > 1) return false;

        return decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }
}
