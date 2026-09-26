using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
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

    private sealed class NullSettings : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }

    private sealed class NoVies : IViesService
    {
        public Task<ViesResult> LookupAsync(string vatCode) =>
            Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
    }

    private sealed class RecordedAzureService : ExpenseOcrService
    {
        private readonly string _json;

        public RecordedAzureService(string json, IDbContextFactory<NordicBeesERPContext> factory)
            : base(factory, new NoVies(), new NullSettings(), NullLogger<ExpenseOcrService>.Instance)
        {
            _json = json;
        }

        protected override Task<string?> AnalyzeInvoiceAsync(string base64, string fileName, OcrResultDto result) =>
            Task.FromResult<string?>(_json);
    }

    private Task<OcrResultDto> ProcessAsync(string json) =>
        new RecordedAzureService(json, _fixture.Factory).ProcessAsync("", "fixture.pdf");

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

    [Fact]
    public async Task ProcessAsync_NotConfigured_ReturnsWithoutParsing()
    {
        var service = new NotConfiguredService(_fixture.Factory);

        var result = await service.ProcessAsync("", "fixture.pdf");

        Assert.False(result.Success);
        Assert.Null(result.RawJson);
        Assert.Empty(result.Lines);
    }

    private sealed class NotConfiguredService : ExpenseOcrService
    {
        public NotConfiguredService(IDbContextFactory<NordicBeesERPContext> factory)
            : base(factory, new NoVies(), new NullSettings(), NullLogger<ExpenseOcrService>.Instance)
        {
        }

        protected override Task<string?> AnalyzeInvoiceAsync(string base64, string fileName, OcrResultDto result)
        {
            result.Diagnostics.AzureError = "Azure DI kredencialai nesukonfigūruoti";
            return Task.FromResult<string?>(null);
        }
    }
}
