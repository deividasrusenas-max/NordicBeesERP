using System.Text.Json;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7(a) — the printed text (<c>content</c>) is read next to every typed numeric value
/// (header totals, line quantity, unit price, line amount) and kept in the DTO for detection only (D-041).
/// The typed values themselves are read exactly as before. Pure tests on hand-built JSON fragments.
/// </summary>
public class OcrNumberReadsTests
{
    private static JsonElement Obj(object value) => JsonSerializer.SerializeToElement(value);

    // ---------- header totals ----------

    [Fact]
    public void HeaderTotals_ReadValuesAndPrintedText()
    {
        var fields = OcrFixtures.Fields(OcrFixtures.Asf0021438());
        var result = new OcrResultDto();

        OcrNumberReads.ReadHeaderTotals(fields, result);

        Assert.Equal(934.22m, result.AmountExclVat);
        Assert.Equal(196.18m, result.VatAmount);
        Assert.Equal(1130.40m, result.AmountInclVat);
        Assert.Equal(new OcrNumberRead("934,22", 934.22m, 934.22m), result.SubTotalRead);
        Assert.Equal(new OcrNumberRead("196,18", 196.18m, 196.18m), result.TotalTaxRead);
        Assert.Equal(new OcrNumberRead("1 130,40", 1130.40m, 1130.40m), result.InvoiceTotalRead);
    }

    [Fact]
    public void HeaderTotals_AmountsConfidenceComesFromInvoiceTotal()
    {
        var result = new OcrResultDto();

        OcrNumberReads.ReadHeaderTotals(OcrFixtures.Fields(OcrFixtures.Asf0021438()), result);

        Assert.Equal(90, result.Confidence.Amounts);
    }

    [Fact]
    public void HeaderTotals_StoredIsRoundedToCents_TypedIsNot()
    {
        var fields = Obj(new { SubTotal = OcrFixtures.Cur("934,2249", 934.2249) });
        var result = new OcrResultDto();

        OcrNumberReads.ReadHeaderTotals(fields, result);

        Assert.Equal(934.2249m, result.SubTotalRead!.Typed);
        Assert.Equal(934.22m, result.SubTotalRead.Stored);
        Assert.Equal(result.SubTotalRead.Stored, result.AmountExclVat);
    }

    [Fact]
    public void HeaderTotals_AbsentOrNullFields_LeaveTheDtoUntouched()
    {
        var fields = Obj(new { SubTotal = (object?)null, InvoiceTotal = new { content = "12,00" } });
        var result = new OcrResultDto();

        OcrNumberReads.ReadHeaderTotals(fields, result);

        Assert.Equal(0m, result.AmountExclVat);
        Assert.Equal(0m, result.VatAmount);
        Assert.Equal(0m, result.AmountInclVat);
        Assert.Null(result.SubTotalRead);
        Assert.Null(result.TotalTaxRead);
        Assert.Null(result.InvoiceTotalRead); // printed text without a typed value: nothing to compare
    }

    // ---------- lines ----------

    [Fact]
    public void Line1_Asf0021438_QuantityRead3_PrintedThreeThousandEightHundredEightyEight()
    {
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(LineFields(OcrFixtures.Asf0021438(), 0), line);

        Assert.Equal(3m, line.Quantity);
        Assert.Equal(new OcrNumberRead("3 888,000", 3m, 3m), line.QuantityRead);
        Assert.Equal(0.2066m, line.UnitPrice);
        Assert.Equal(new OcrNumberRead("0,2066", 0.2066m, 0.2066m), line.UnitPriceRead);
        Assert.Equal(972.00m, line.AmountExclVat);
        Assert.Equal(new OcrNumberRead("972,00", 972.00m, 972.00m), line.AmountRead);
    }

    [Fact]
    public void Line2_Asf0021438_QuantityRead9000_PrintedNine()
    {
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(LineFields(OcrFixtures.Asf0021438(), 1), line);

        Assert.Equal(9000m, line.Quantity);
        Assert.Equal(new OcrNumberRead("9,000", 9000m, 9000m), line.QuantityRead);
        Assert.Equal(new OcrNumberRead("158,40", 158.40m, 158.40m), line.AmountRead);
    }

    [Fact]
    public void Line_NetIsTheFallbackForAmount_AndItsTextIsTheRead()
    {
        var fields = Obj(new { Net = OcrFixtures.Cur("12,50", 12.5) });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Equal(12.5m, line.AmountExclVat);
        Assert.Equal(new OcrNumberRead("12,50", 12.5m, 12.5m), line.AmountRead);
    }

    [Fact]
    public void Line_AmountWins_OverNet_WhenNonZero()
    {
        var fields = Obj(new { Amount = OcrFixtures.Cur("10,00", 10.0), Net = OcrFixtures.Cur("99,00", 99.0) });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Equal(10m, line.AmountExclVat);
        Assert.Equal("10,00", line.AmountRead!.Printed);
    }

    [Fact]
    public void Line_ZeroAmount_FallsBackToNet_LikeBefore()
    {
        var fields = Obj(new { Amount = OcrFixtures.Cur("0,00", 0.0), Net = OcrFixtures.Cur("5,00", 5.0) });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Equal(5m, line.AmountExclVat);
        Assert.Equal("5,00", line.AmountRead!.Printed);
    }

    [Fact]
    public void Line_MissingFields_NoValuesNoReads()
    {
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(Obj(new { Description = OcrFixtures.Text("x") }), line);

        Assert.Null(line.Quantity);
        Assert.Null(line.UnitPrice);
        Assert.Equal(0m, line.AmountExclVat);
        Assert.Null(line.QuantityRead);
        Assert.Null(line.UnitPriceRead);
        Assert.Null(line.AmountRead);
    }

    [Fact]
    public void Line_FieldWithoutContent_HasAnEmptyPrintedText()
    {
        var fields = Obj(new { Quantity = new { valueNumber = 2.0 } });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Equal(2m, line.Quantity);
        Assert.Equal(new OcrNumberRead("", 2m, 2m), line.QuantityRead);
    }

    [Fact]
    public void Line_PascalCasePropertyNames_AreStillAccepted()
    {
        var fields = Obj(new
        {
            Quantity = new { Content = "2", ValueNumber = 2.0 },
            UnitPrice = new { Content = "1,50", ValueCurrency = new { Amount = 1.5 } },
            Amount = new { Content = "3,00", ValueCurrency = new { Amount = 3.0 } }
        });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Equal(2m, line.Quantity);
        Assert.Equal(1.5m, line.UnitPrice);
        Assert.Equal(3m, line.AmountExclVat);
        Assert.Equal("1,50", line.UnitPriceRead!.Printed);
    }

    [Fact]
    public void Line_NullAndWrongKindFields_DoNotThrow()
    {
        var fields = Obj(new
        {
            Quantity = (object?)null,
            UnitPrice = new { content = "1,00", valueCurrency = new { amount = "not a number" } },
            Amount = new { content = "1,00", valueCurrency = (object?)null }
        });
        var line = new OcrLineDto();

        OcrNumberReads.ReadLineNumbers(fields, line);

        Assert.Null(line.Quantity);
        Assert.Null(line.UnitPrice);
        Assert.Equal(0m, line.AmountExclVat);
    }

    private static JsonElement LineFields(string response, int index) =>
        OcrFixtures.Fields(response).GetProperty("Items").GetProperty("valueArray")[index].GetProperty("valueObject");
}
