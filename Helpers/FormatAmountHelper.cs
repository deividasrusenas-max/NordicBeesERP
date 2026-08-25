using System.Globalization;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Project-wide read-only money/amount display convention.
/// Trims trailing zeros with no forced decimals: 100.00m -> "100",
/// 3.40m -> "3,4", 412.61m -> "412,61" (lt-LT comma decimal separator).
/// Used ONLY for read-only displayed amounts, NEVER inside editable
/// MudNumericField bindings.
/// </summary>
public static class FormatAmountHelper
{
    public static string FormatAmount(decimal value)
        => value.ToString("0.####", CultureInfo.GetCultureInfo("lt-LT"));

    /// <summary>
    /// Formats an amount that is stored POSITIVE but must be displayed NEGATIVE
    /// (credit-note line/total values), suppressing the minus sign when the
    /// magnitude is zero so it never renders as "-0".
    /// </summary>
    public static string FormatNegatedAmount(decimal value)
        => FormatAmount(value == 0m ? 0m : -value);

    /// <summary>
    /// Formats an ALREADY-SIGNED amount for display, suppressing a leading minus
    /// sign on zero (e.g. a client-side credit model that already carried a
    /// negative value) so it never renders as "-0".
    /// </summary>
    public static string FormatSignedAmount(decimal value)
        => FormatAmount(value == 0m ? 0m : value);

    /// <summary>
    /// Formats a decimal value by trimming trailing zeros while preserving up to
    /// maxDecimals real decimal digits, using the app's lt-LT display culture.
    /// 5.000m with maxDecimals=3 -> "5" | 5.250m with maxDecimals=3 -> "5,25" |
    /// 5.500m with maxDecimals=3 -> "5,5".
    /// </summary>
    public static string Trim(decimal value, int maxDecimals = 3)
        => value.ToString($"0.{new string('#', maxDecimals)}", CultureInfo.GetCultureInfo("lt-LT"));

    public static string Trim(decimal? value, int maxDecimals = 3)
        => value.HasValue ? Trim(value.Value, maxDecimals) : "";
}
