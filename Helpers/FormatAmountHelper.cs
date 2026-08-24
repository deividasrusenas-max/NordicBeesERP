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
}
