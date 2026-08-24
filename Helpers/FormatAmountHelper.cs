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
}
