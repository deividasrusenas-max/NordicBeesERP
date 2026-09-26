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
            OcrNumberReads.ReadLineNumbers(item.GetProperty("valueObject"), line);
            result.Lines.Add(line);
        }
        return result;
    }

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
