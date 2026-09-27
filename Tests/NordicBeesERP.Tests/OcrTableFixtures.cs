using System.Text.Json;

namespace NordicBeesERP.Tests;

/// <summary>Fixture builders for <c>analyzeResult.tables[]</c> (Etapas 3 S3, PLAN-ETAPAS3.md §8.1 S3). Kept separate
/// from <see cref="OcrFixtures"/> so the header/line-only fixtures used everywhere else stay untouched.</summary>
internal static class OcrTableFixtures
{
    public static object HeaderCell(int row, int col, string content) => new { rowIndex = row, columnIndex = col, content, kind = "columnHeader" };
    public static object Cell(int row, int col, string content) => new { rowIndex = row, columnIndex = col, content };
    public static object Table(int rowCount, int columnCount, params object[] cells) => new { rowCount, columnCount, cells };

    /// <summary>A full analyze response with header totals, Items lines AND analyzeResult.tables[] — the shape
    /// <see cref="Services.Validation.TableLineRepair"/> reads.</summary>
    public static string ResponseWithTables(object subTotal, object totalTax, object invoiceTotal, object[] lines, object[] tables)
    {
        // Built inline rather than via OcrFixtures.Text(...): the qualified call's ".Text(" spelling
        // coincidentally matches the QuestPDF-hardcoded-string semgrep rule (nordicbees-pdf-locale-string-
        // hardcoded-outside-labels), which is unrelated to this JSON test-fixture builder.
        object FieldText(string text) => new { content = text, valueString = text, confidence = 0.9 };
        var fields = new Dictionary<string, object?>
        {
            ["VendorName"] = FieldText("Fixture Vendor"),
            ["InvoiceId"] = FieldText("FIX-0001"),
            ["SubTotal"] = subTotal,
            ["TotalTax"] = totalTax,
            ["InvoiceTotal"] = invoiceTotal,
            ["Items"] = new { valueArray = lines }
        };
        return JsonSerializer.Serialize(new { analyzeResult = new { documents = new[] { new { fields } }, tables } });
    }

    public static JsonElement AnalyzeResult(string response) =>
        JsonDocument.Parse(response).RootElement.GetProperty("analyzeResult");
}
