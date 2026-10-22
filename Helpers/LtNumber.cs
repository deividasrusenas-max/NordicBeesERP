using System.Globalization;

namespace NordicBeesERP.Helpers;

/// <summary>
/// The one parser / formatter for numbers a person types or reads in the ERP. The decimal separator is ALWAYS the comma,
/// whatever the browser or server locale is; input never depends on <see cref="CultureInfo.CurrentCulture"/>.
/// Machine formats (SQL parameters, JSON, XLSX numeric cells, bank statements, OCR) do NOT go through here.
/// </summary>
public static class LtNumber
{
    /// <summary>
    /// "11257,24" / "11257.24" -> 11257.24; "1 125,50" / "1.125,50" / "1,125.50" -> 1125.50.
    /// Spaces and NBSP are stripped. Only "," or only "." = decimal separator. Both present = the LAST one is decimal and
    /// the other kind is thousands (and must not repeat after it). Empty, garbage, or a separator repeated with no other
    /// kind ("1.234.567") = false: never guess, never a silent 100x.
    /// </summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim().Replace(" ", "").Replace("\u00A0", "").Replace("\u202F", "").Replace('\u2212', '-');
        if (s.Length == 0) return false;

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        var commas = s.Count(c => c == ',');
        var dots = s.Count(c => c == '.');

        char? decimalSep = null;
        if (commas > 0 && dots > 0)
        {
            decimalSep = lastComma > lastDot ? ',' : '.';
            var thousandsSep = decimalSep == ',' ? '.' : ',';
            // exactly one decimal separator, and every thousands separator must precede it
            if ((decimalSep == ',' ? commas : dots) != 1) return false;
            if ((thousandsSep == ',' ? lastComma : lastDot) > (decimalSep == ',' ? lastComma : lastDot)) return false;
            s = s.Replace(thousandsSep.ToString(), "");
        }
        else if (commas > 0 || dots > 0)
        {
            if (commas + dots > 1) return false;
            decimalSep = commas > 0 ? ',' : '.';
        }

        if (decimalSep == ',') s = s.Replace(',', '.');

        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parse for double fields (same rules).</summary>
    public static bool TryParseDouble(string? text, out double value)
    {
        value = 0;
        if (!TryParseDecimal(text, out var d)) return false;
        value = (double)d;
        return true;
    }

    /// <summary>Decimal with a comma, <paramref name="decimals"/> places. Grouped = thousands separated by NBSP ("1 125,50").</summary>
    public static string Format(decimal value, int decimals = 2, bool grouped = true) =>
        value.ToString((grouped ? "N" : "F") + decimals, LtCulture.Instance);

    public static string Format(double value, int decimals = 2, bool grouped = true) =>
        value.ToString((grouped ? "N" : "F") + decimals, LtCulture.Instance);
}
