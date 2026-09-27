using System.Text.Json;

namespace NordicBeesERP.Tests;

/// <summary>Fixture builder for ZERO_VAT / ZERO_VAT_NO_BASIS integration tests (Etapas 3 S4, PLAN-ETAPAS3.md §8.1
/// S4): needs a resolvable supplier country (<c>VendorAddress.countryRegion</c>) and a searchable
/// <c>analyzeResult.content</c>, neither of which <see cref="OcrFixtures.Response"/> sets.</summary>
internal static class OcrZeroVatFixtures
{
    public static string Response(object subTotal, object totalTax, object invoiceTotal, string countryRegion, string documentText, params object[] lines)
    {
        // Built inline rather than via OcrFixtures.Text(...): see OcrTableFixtures.cs's identical note — the
        // qualified call's ".Text(" spelling coincidentally matches an unrelated QuestPDF semgrep rule.
        object FieldText(string text) => new { content = text, valueString = text, confidence = 0.9 };
        var fields = new Dictionary<string, object?>
        {
            ["VendorName"] = FieldText("Fixture Vendor"),
            ["InvoiceId"] = FieldText("FIX-0001"),
            ["VendorAddress"] = new { valueAddress = new { countryRegion } },
            ["SubTotal"] = subTotal,
            ["TotalTax"] = totalTax,
            ["InvoiceTotal"] = invoiceTotal,
            ["Items"] = new { valueArray = lines }
        };
        return JsonSerializer.Serialize(new { analyzeResult = new { content = documentText, documents = new[] { new { fields } } } });
    }
}
