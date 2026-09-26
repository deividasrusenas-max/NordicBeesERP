using System.Text.Json;

namespace NordicBeesERP.Services;

/// <summary>
/// Reads Azure DI's typed numeric values together with the printed text next to them (<c>content</c>) so
/// <c>LocaleNumberCandidates</c> can compare them (OCR Etapas 1 S7, D-038 Q6, D-041: detection only, values are never
/// replaced). The typed values are read exactly as <see cref="ExpenseOcrService.ProcessAsync"/> always read them
/// (<c>(decimal)double</c>; header totals rounded to cents) — the reads only add the text.
/// </summary>
public static class OcrNumberReads
{
    /// <summary>Header totals: SubTotal → AmountExclVat, TotalTax → VatAmount, InvoiceTotal → AmountInclVat (+ Amounts confidence).</summary>
    public static void ReadHeaderTotals(JsonElement invoice, OcrResultDto result)
    {
        if (TryField(invoice, "SubTotal", out var subTotal) && CurrencyAmount(subTotal) is { } sub)
        {
            result.AmountExclVat = Math.Round(sub, 2);
            result.SubTotalRead = FromCurrency(subTotal, 2);
        }

        if (TryField(invoice, "TotalTax", out var totalTax) && CurrencyAmount(totalTax) is { } tax)
        {
            result.VatAmount = Math.Round(tax, 2);
            result.TotalTaxRead = FromCurrency(totalTax, 2);
        }

        if (TryField(invoice, "InvoiceTotal", out var invoiceTotal) && CurrencyAmount(invoiceTotal) is { } total)
        {
            result.AmountInclVat = Math.Round(total, 2);
            result.InvoiceTotalRead = FromCurrency(invoiceTotal, 2);
            if (invoiceTotal.TryGetProperty("confidence", out var confidence))
                result.Confidence.Amounts = ExpenseOcrService.ToConfidencePercent((float)confidence.GetDouble());
        }
    }

    /// <summary>
    /// One line's Quantity, UnitPrice and Amount (Net when Amount gave nothing) — value and printed text.
    /// <paramref name="fields"/> is the line's <c>valueObject</c>.
    /// </summary>
    public static void ReadLineNumbers(JsonElement fields, OcrLineDto line)
    {
        if (TryField(fields, "Quantity", out var qtyField) && Number(qtyField) is { } qty)
        {
            line.Quantity = qty;
            line.QuantityRead = FromNumber(qtyField);
        }

        if (TryField(fields, "UnitPrice", out var unitPriceField) && CurrencyAmount(unitPriceField) is { } unitPrice)
        {
            line.UnitPrice = unitPrice;
            line.UnitPriceRead = FromCurrency(unitPriceField);
        }

        if (TryField(fields, "Amount", out var amountField) && CurrencyAmount(amountField) is { } amount)
        {
            line.AmountExclVat = amount;
            line.AmountRead = FromCurrency(amountField);
        }

        // Net field as a fallback for Amount
        if (line.AmountExclVat == 0 && TryField(fields, "Net", out var netField) && CurrencyAmount(netField) is { } net)
        {
            line.AmountExclVat = net;
            line.AmountRead = FromCurrency(netField);
        }
    }

    /// <summary>The printed text and typed value of a <c>valueCurrency</c> field; null without a typed value. <paramref name="decimals"/>: rounding of the stored value.</summary>
    public static OcrNumberRead? FromCurrency(JsonElement field, int? decimals = null) =>
        CurrencyAmount(field) is { } typed ? Build(field, typed, decimals) : null;

    /// <summary>The printed text and typed value of a <c>valueNumber</c> field; null without a typed value.</summary>
    public static OcrNumberRead? FromNumber(JsonElement field) =>
        Number(field) is { } typed ? Build(field, typed, null) : null;

    private static OcrNumberRead Build(JsonElement field, decimal typed, int? decimals) =>
        new(Str(field, "content") ?? "", typed, decimals is { } d ? Math.Round(typed, d) : typed);

    private static decimal? CurrencyAmount(JsonElement field)
    {
        if (Prop(field, "valueCurrency") is not { ValueKind: JsonValueKind.Object } currency) return null;
        return Prop(currency, "amount") is { ValueKind: JsonValueKind.Number } amount ? (decimal)amount.GetDouble() : null;
    }

    private static decimal? Number(JsonElement field) =>
        Prop(field, "valueNumber") is { ValueKind: JsonValueKind.Number } number ? (decimal)number.GetDouble() : null;

    // A field that exists and is not JSON null (as ExpenseOcrService's own helper reads it).
    private static bool TryField(JsonElement parent, string name, out JsonElement field)
    {
        field = default;
        if (Prop(parent, name) is not { } found) return false;
        field = found;
        return true;
    }

    // Azure's JSON uses camelCase; the service has always also accepted PascalCase.
    private static JsonElement? Prop(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null) return value;
        var pascal = char.ToUpperInvariant(name[0]) + name[1..];
        if (element.TryGetProperty(pascal, out value) && value.ValueKind != JsonValueKind.Null) return value;
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return element.TryGetProperty(camel, out value) && value.ValueKind != JsonValueKind.Null ? value : null;
    }

    private static string? Str(JsonElement field, string name) =>
        Prop(field, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}
