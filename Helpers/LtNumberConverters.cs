
namespace NordicBeesERP.Helpers;

/// <summary>
/// The shared MudBlazor converters for every MudNumericField: text -> number goes through <see cref="LtNumber"/>
/// (comma or dot, never browser-dependent), number -> text is always a comma. Unparseable text is reported as a
/// conversion error instead of a silent 0 or 100x. Use together with <c>Culture="@LtCulture.Instance"</c>:
/// <c>Converter="@LtNumberConverters.Decimal"</c>.
/// </summary>
public static class LtNumberConverters
{
    public static readonly MudBlazor.Converter<decimal> Decimal = new LtDecimalConverter();
    public static readonly MudBlazor.Converter<decimal?> NullableDecimal = new LtNullableDecimalConverter();
    public static readonly MudBlazor.Converter<double> Double = new LtDoubleConverter();
    public static readonly MudBlazor.Converter<double?> NullableDouble = new LtNullableDoubleConverter();

    private static string Show(decimal v, string? format) => v.ToString(string.IsNullOrEmpty(format) ? "0.############################" : format, LtCulture.Instance);
    private static string Show(double v, string? format) => v.ToString(string.IsNullOrEmpty(format) ? "0.################" : format, LtCulture.Instance);

    private sealed class LtDecimalConverter : MudBlazor.Converter<decimal>
    {
        public LtDecimalConverter()
        {
            SetFunc = v => Show(v, Format);
            GetFunc = text =>
            {
                if (LtNumber.TryParseDecimal(text, out var v)) return v;
                UpdateGetError("Neteisingas skaičius");
                return default;
            };
        }
    }

    private sealed class LtNullableDecimalConverter : MudBlazor.Converter<decimal?>
    {
        public LtNullableDecimalConverter()
        {
            SetFunc = v => v.HasValue ? Show(v.Value, Format) : string.Empty;
            GetFunc = text =>
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (LtNumber.TryParseDecimal(text, out var v)) return v;
                UpdateGetError("Neteisingas skaičius");
                return null;
            };
        }
    }

    private sealed class LtDoubleConverter : MudBlazor.Converter<double>
    {
        public LtDoubleConverter()
        {
            SetFunc = v => Show(v, Format);
            GetFunc = text =>
            {
                if (LtNumber.TryParseDouble(text, out var v)) return v;
                UpdateGetError("Neteisingas skaičius");
                return default;
            };
        }
    }

    private sealed class LtNullableDoubleConverter : MudBlazor.Converter<double?>
    {
        public LtNullableDoubleConverter()
        {
            SetFunc = v => v.HasValue ? Show(v.Value, Format) : string.Empty;
            GetFunc = text =>
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (LtNumber.TryParseDouble(text, out var v)) return v;
                UpdateGetError("Neteisingas skaičius");
                return null;
            };
        }
    }
}
