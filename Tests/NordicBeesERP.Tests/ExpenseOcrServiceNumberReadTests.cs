using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7 through <see cref="ExpenseOcrService.ProcessAsync"/> itself: a recorded Azure response is fed in
/// place of the Azure call (the virtual <c>AnalyzeInvoiceAsync</c> seam), so the wiring — not just the helpers — is
/// exercised. Integration tests against nordic_bees_erp_test (the supplier match reads partners).
/// </summary>
public class ExpenseOcrServiceNumberReadTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseOcrServiceNumberReadTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private Task<OcrResultDto> ProcessAsync(string json) =>
        new RecordedAzureOcrService(json, _fixture.Factory).ProcessAsync("", "fixture.pdf");

    [Fact]
    public async Task ProcessAsync_KeepsTheRawJson_AndReadsHeaderTotalsWithTheirPrintedText()
    {
        var json = OcrFixtures.Asf0021438();

        var result = await ProcessAsync(json);

        Assert.True(result.Success);
        Assert.Equal(json, result.RawJson);
        Assert.Equal(934.22m, result.AmountExclVat);
        Assert.Equal(196.18m, result.VatAmount);
        Assert.Equal(1130.40m, result.AmountInclVat);
        Assert.Equal("934,22", result.SubTotalRead!.Printed);
        Assert.Equal("196,18", result.TotalTaxRead!.Printed);
        Assert.Equal("1 130,40", result.InvoiceTotalRead!.Printed);
    }

    [Fact]
    public async Task ProcessAsync_ReadsLineNumbersWithTheirPrintedText()
    {
        var result = await ProcessAsync(OcrFixtures.Asf0021438());

        var line = Assert.Single(result.Lines, l => l.Description == "Kuras A");
        Assert.Equal(3m, line.Quantity);
        Assert.Equal("3 888,000", line.QuantityRead!.Printed);
        Assert.Equal(0.2066m, line.UnitPrice);
        Assert.Equal("0,2066", line.UnitPriceRead!.Printed);
        Assert.Equal(972.00m, line.AmountExclVat);
        Assert.Equal("972,00", line.AmountRead!.Printed);
    }

    // ---------- S7(b): detection through ProcessAsync ----------

    [Fact]
    public async Task ProcessAsync_Asf0021438_PreviewFlags_MisreadAndAmbiguous_ValuesUntouched()
    {
        var result = await ProcessAsync(OcrFixtures.Asf0021438());

        Assert.Contains(OcrFlag.NumberMisread, result.Flags);
        Assert.Contains(OcrFlag.NumberAmbiguous, result.Flags);
        var line = Assert.Single(result.Lines, l => l.Description == "Kuras A");
        Assert.Equal(3m, line.Quantity); // Azure's value, never replaced (D-038 Q6)
    }

    [Fact]
    public async Task ProcessAsync_CleanDocument_NoNumberFlags()
    {
        var json = OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("Prekė", OcrFixtures.Num("2,00", 2), OcrFixtures.Cur("50,00", 50), OcrFixtures.Cur("100,00", 100)));

        var result = await ProcessAsync(json);

        Assert.DoesNotContain(OcrFlag.NumberMisread, result.Flags);
        Assert.DoesNotContain(OcrFlag.NumberAmbiguous, result.Flags);
    }

    [Fact]
    public async Task ProcessAsync_DetectionRunsBeforeReconcile_ALineTheReconcileRemovesStillFlags()
    {
        // header 100,00; line A alone is 150,00 → lines exceed the header, so the reconcile step's first pass removes
        // every zero-amount line — here line Z, whose quantity Azure misread („3 888,000" → 3)
        var json = OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("Prekė A", OcrFixtures.Num("1,00", 1), OcrFixtures.Cur("150,00", 150), OcrFixtures.Cur("150,00", 150)),
            OcrFixtures.Line("Prekė Z", OcrFixtures.Num("3 888,000", 3), null, OcrFixtures.Cur("0,00", 0)));

        var result = await ProcessAsync(json);

        Assert.DoesNotContain(result.Lines, l => l.Description == "Prekė Z"); // removed by the reconcile step
        Assert.Contains(OcrFlag.NumberMisread, result.Flags);                   // …but detection had already seen it
    }

    [Fact]
    public async Task ProcessAsync_NotConfigured_ReturnsWithoutParsing()
    {
        var service = new RecordedAzureOcrService(null, _fixture.Factory);

        var result = await service.ProcessAsync("", "fixture.pdf");

        Assert.False(result.Success);
        Assert.Null(result.RawJson);
        Assert.Empty(result.Lines);
    }
}
