using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;

namespace NordicBeesERP.Tests;

/// <summary>
/// Hand-built Azure DI <c>prebuilt-invoice</c> response fragments (no corpus — D-038 Q9, the corpus holds personal
/// data). The strings are the real ones from ASF0021438 (PLAN-ETAPAS1 §3.1): Azure read „3 888,000" as 3 and „9,000"
/// as 9000, and both line amounts are from the „Suma su PVM" column (D-023).
/// </summary>
internal static class OcrFixtures
{
    public static object Cur(string content, double amount) =>
        new { content, valueCurrency = new { amount, currencyCode = "EUR" }, confidence = 0.9 };

    public static object Num(string content, double number) => new { content, valueNumber = number, confidence = 0.9 };

    public static object Text(string text) => new { content = text, valueString = text, confidence = 0.9 };

    public static object Line(string description, object? quantity, object? unitPrice, object? amount)
    {
        var fields = new Dictionary<string, object?> { ["Description"] = Text(description) };
        if (quantity != null) fields["Quantity"] = quantity;
        if (unitPrice != null) fields["UnitPrice"] = unitPrice;
        if (amount != null) fields["Amount"] = amount;
        return new { valueObject = fields, confidence = 0.9 };
    }

    /// <summary>A full analyze response: header totals plus the given lines.</summary>
    public static string Response(object subTotal, object totalTax, object invoiceTotal, params object[] lines)
    {
        var fields = new Dictionary<string, object?>
        {
            ["VendorName"] = Text("Fixture Vendor"),
            ["InvoiceId"] = Text("FIX-0001"),
            ["SubTotal"] = subTotal,
            ["TotalTax"] = totalTax,
            ["InvoiceTotal"] = invoiceTotal,
            ["Items"] = new { valueArray = lines }
        };
        return JsonSerializer.Serialize(new { analyzeResult = new { documents = new[] { new { fields } } } });
    }

    /// <summary>A DTO filled exactly as ProcessAsync fills it (values and printed-text reads, same helpers), without the reconcile step.</summary>
    public static OcrResultDto Dto(string response)
    {
        var fields = Fields(response);
        var result = new OcrResultDto();
        OcrNumberReads.ReadHeaderTotals(fields, result);
        foreach (var item in fields.GetProperty("Items").GetProperty("valueArray").EnumerateArray())
        {
            var line = new OcrLineDto();
            var lineFields = item.GetProperty("valueObject");
            line.Description = FieldText(lineFields, "Description");
            if (string.IsNullOrEmpty(line.Description)) line.Description = FieldText(lineFields, "ProductCode");
            if (string.IsNullOrEmpty(line.Description)) line.Description = FieldText(lineFields, "ProductDescription");
            OcrNumberReads.ReadLineNumbers(lineFields, line);
            result.Lines.Add(line);
        }
        return result;
    }

    // Same "valueString or content" fallback ExpenseOcrService.ProcessAsync's field readers use for text fields
    // (D-045's own pattern, e.g. SupplierIdentifiers above).
    private static string FieldText(JsonElement fields, string name) =>
        fields.TryGetProperty(name, out var f)
            ? (f.TryGetProperty("valueString", out var vs) ? vs.GetString() : f.TryGetProperty("content", out var c) ? c.GetString() : "") ?? ""
            : "";

    public static JsonElement Fields(string response)
    {
        var root = JsonDocument.Parse(response).RootElement;
        return root.GetProperty("analyzeResult").GetProperty("documents")[0].GetProperty("fields");
    }

    /// <summary>ASF0021438 as Azure returned it: header correct, line 1 quantity 3 (printed „3 888,000"), line 2 quantity 9000 (printed „9,000").</summary>
    public static string Asf0021438() => Response(
        Cur("934,22", 934.22), Cur("196,18", 196.18), Cur("1 130,40", 1130.40),
        Line("Kuras A", Num("3 888,000", 3), Cur("0,2066", 0.2066), Cur("972,00", 972.00)),
        Line("Kuras B", Num("9,000", 9000), Cur("14,5456", 14.5456), Cur("158,40", 158.40)));

    /// <summary>
    /// The supplier identifiers a golden-file snapshot (Etapas 3 S2, PLAN-ETAPAS3.md §7.2) reports alongside header
    /// totals and lines: VendorName's printed text, VendorTaxId cleaned the same way <c>ExpenseOcrService</c> does
    /// (spaces/dashes/dots stripped — the one place this test helper duplicates a private production helper rather
    /// than calling it, because it is private), and the registration code via the same public
    /// <see cref="ExpenseOcrService.ExtractSupplierCompanyCode"/> wrapper production uses.
    /// </summary>
    public static (string VatCode, string CompanyCode, string Name) SupplierIdentifiers(string response, CompanySettings? settings = null, string? customerVatCode = null)
    {
        var root = JsonDocument.Parse(response).RootElement;
        var fields = Fields(response);
        string Str(string field) => fields.TryGetProperty(field, out var f)
            ? (f.TryGetProperty("valueString", out var vs) ? vs.GetString() : f.TryGetProperty("content", out var c) ? c.GetString() : "") ?? ""
            : "";
        var vatCode = Str("VendorTaxId").Replace(" ", "").Replace("-", "").Replace(".", "").Trim();
        var name = Str("VendorName");
        var extraction = ExpenseOcrService.ExtractSupplierCompanyCode(root, settings ?? new CompanySettings(), customerVatCode);
        return (vatCode, extraction.Code, name);
    }

    /// <summary>
    /// The normalised projection a golden-file test snapshots (Etapas 3 S2, PLAN-ETAPAS3.md §7.2): header totals, VAT
    /// rate, per-line description/quantity/unit price/net/net-derived, supplier identifiers — never the raw Azure JSON
    /// (a raw-response snapshot would only prove "Azure didn't change", not "the mapping is still correct").
    /// </summary>
    public static object Snapshot(OcrResultDto dto, (string VatCode, string CompanyCode, string Name) supplier) => new
    {
        Supplier = new { supplier.VatCode, supplier.CompanyCode, supplier.Name },
        Header = new { dto.AmountExclVat, dto.VatRate, dto.VatAmount, dto.AmountInclVat },
        Lines = dto.Lines.Select(l => new
        {
            l.Description,
            l.Quantity,
            l.UnitPrice,
            l.AmountExclVat,
            l.NetDerived
        }).ToList()
    };

    /// <summary>The full golden-file snapshot for a stored Azure response: parses it exactly as <see cref="Dto"/> does, then adds supplier identifiers.</summary>
    public static object SnapshotOf(string rawJson, CompanySettings? settings = null, string? customerVatCode = null) =>
        Snapshot(Dto(rawJson), SupplierIdentifiers(rawJson, settings, customerVatCode));
}

/// <summary>An OCR service whose Azure call returns a recorded response, so <c>ProcessAsync</c> runs end to end.</summary>
internal sealed class RecordedAzureOcrService : ExpenseOcrService
{
    private sealed class NoVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) =>
            Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private sealed class NullSettings : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }

    private readonly string? _json;

    public RecordedAzureOcrService(string? json, IDbContextFactory<NordicBeesERPContext> factory)
        : base(factory, new NoVies(), new NullSettings(), NullLogger<ExpenseOcrService>.Instance)
    {
        _json = json;
    }

    protected override Task<string?> AnalyzeInvoiceAsync(string base64, string fileName, OcrResultDto result)
    {
        if (_json == null) result.Diagnostics.AzureError = "Azure DI kredencialai nesukonfigūruoti";
        return Task.FromResult(_json);
    }
}
