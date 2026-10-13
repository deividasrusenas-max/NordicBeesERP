using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NordicBeesERP.Components.Pages.Warehouse;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>Renders the real BucketWeighingGrid with a fake interval service (no DB).</summary>
public class BucketWeighingGridTests : IDisposable
{
    private readonly TestContext _ctx = new();

    public BucketWeighingGridTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton<BucketTareCalculator>();
        _ctx.Services.AddSingleton<IBucketTareService>(new FakeTareService());
        _ctx.RenderComponent<MudBlazor.MudPopoverProvider>();
    }

    public void Dispose()
    {
        _ctx.Dispose();
    }

    private sealed class FakeTareService : IBucketTareService
    {
        public Task<IReadOnlyList<BucketTareRange>> GetRangesAsync(bool includeInactive = false)
            => Task.FromResult<IReadOnlyList<BucketTareRange>>(new List<BucketTareRange>
            {
                new() { Id = 1, MinBruttoKg = 1.00m, MaxBruttoKg = 16.00m, TareKg = 0.40m, SortOrder = 1, IsActive = true },
                new() { Id = 2, MinBruttoKg = 16.01m, MaxBruttoKg = 40.00m, TareKg = 0.80m, SortOrder = 2, IsActive = true },
                new() { Id = 3, MinBruttoKg = 40.01m, MaxBruttoKg = 55.00m, TareKg = 1.10m, SortOrder = 3, IsActive = true }
            });

        public Task<TareResult> GetTareAsync(decimal brutto) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> SaveRangesAsync(IReadOnlyList<BucketTareRange> ranges) => throw new NotSupportedException();
    }

    private IRenderedComponent<BucketWeighingGrid> Render(List<List<BucketEntry>>? received = null, bool isHoney = true)
        => _ctx.RenderComponent<BucketWeighingGrid>(p => p
            .Add(c => c.IsHoney, isHoney)
            .Add(c => c.OnAdd, Microsoft.AspNetCore.Components.EventCallback.Factory.Create<List<BucketEntry>>(this, e => received?.Add(e))));

    private static AngleSharp.Dom.IElement Brutto(IRenderedComponent<BucketWeighingGrid> cut, int row)
        => cut.FindAll("[data-testid=bucket-row]")[row].QuerySelectorAll("input")[0];

    private static AngleSharp.Dom.IElement Tare(IRenderedComponent<BucketWeighingGrid> cut, int row)
        => cut.FindAll("[data-testid=bucket-row]")[row].QuerySelectorAll("input")[1];

    // The component formats with the machine culture (lt-LT uses a decimal comma); tests compare culture-neutrally.
    private static string Value(AngleSharp.Dom.IElement input) => (input.GetAttribute("value") ?? "").Replace(',', '.');

    private static string Netto(IRenderedComponent<BucketWeighingGrid> cut)
        => cut.Find("[data-testid=bucket-netto]").TextContent.Trim().Replace(',', '.');

    [Fact]
    public void Brutto25_ShowsAutoTare08AndNetto242()
    {
        var cut = Render();

        Brutto(cut, 0).Input("25");

        Assert.Equal("0.80", Value(Tare(cut, 0)));
        Assert.Equal("24.20 kg", Netto(cut));
        Assert.Empty(cut.FindAll("[data-testid=tare-manual-marker]"));
    }

    [Fact]
    public void ChangingTare_ShowsMarker_AndResetReturnsAutoTare()
    {
        var cut = Render();
        Brutto(cut, 0).Input("25");

        Tare(cut, 0).Input("1");

        Assert.Single(cut.FindAll("[data-testid=tare-manual-marker]"));
        Assert.Equal("24.00 kg", Netto(cut));

        cut.Find("[data-testid=tare-reset]").Click();

        Assert.Empty(cut.FindAll("[data-testid=tare-manual-marker]"));
        Assert.Equal("0.80", Value(Tare(cut, 0)));
        Assert.Equal("24.20 kg", Netto(cut));
    }

    [Fact]
    public void ManualTare_SurvivesBruttoChange()
    {
        var cut = Render();
        Brutto(cut, 0).Input("25");
        Tare(cut, 0).Input("1");

        Brutto(cut, 0).Input("45"); // auto tare would be 1.10

        // brutto 45 − manual tare 1 (auto would give 43.90)
        Assert.Equal("44.00 kg", Netto(cut));
        Assert.Single(cut.FindAll("[data-testid=tare-manual-marker]"));
    }

    [Fact]
    public void Brutto60_ShowsRangeError_AndBlocksAddUntilManualTare()
    {
        var received = new List<List<BucketEntry>>();
        var cut = Render(received);

        Brutto(cut, 0).Input("60");

        Assert.Contains("Brutto už intervalų ribų (1–55 kg), įveskite tarą ranka", cut.Find("[data-testid=bucket-error]").TextContent);
        Assert.True(cut.Find("[data-testid=bucket-add]").HasAttribute("disabled"));

        Tare(cut, 0).Input("2");

        Assert.Empty(cut.FindAll("[data-testid=bucket-error]"));
        Assert.False(cut.Find("[data-testid=bucket-add]").HasAttribute("disabled"));

        cut.Find("[data-testid=bucket-add]").Click();

        var entry = Assert.Single(Assert.Single(received));
        Assert.Equal(60m, entry.Gross);
        Assert.Equal(2m, entry.Tare);
        Assert.True(entry.TareManual);
    }

    [Fact]
    public void Enter_AddsNextRow_AndFocusesIt()
    {
        var cut = Render();
        Brutto(cut, 0).Input("5");
        var focusBefore = _ctx.JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

        Brutto(cut, 0).KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(2, cut.FindAll("[data-testid=bucket-row]").Count);
        var focusAfter = _ctx.JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
        Assert.True(focusAfter > focusBefore, "Enter must move focus to the next brutto field");
    }

    [Fact]
    public void FirstBruttoField_IsFocusedOnLoad()
    {
        Render();

        Assert.Contains(_ctx.JSInterop.Invocations, i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TareField_IsSkippedInTabOrder()
    {
        var cut = Render();

        Assert.Equal("-1", Tare(cut, 0).GetAttribute("tabindex"));
        Assert.Null(Brutto(cut, 0).GetAttribute("tabindex"));
    }

    [Fact]
    public void Totals_AreSumsOfFilledRows()
    {
        var cut = Render();
        Brutto(cut, 0).Input("5");
        Brutto(cut, 0).KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Brutto(cut, 1).Input("25");
        Brutto(cut, 1).KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Brutto(cut, 2).Input("50");

        var totals = cut.Find("[data-testid=bucket-totals]").TextContent;

        // brutto 80.00, tara 0.40 + 0.80 + 1.10 = 2.30, netto 77.70
        Assert.Contains("Kibirų: 3", Normalize(totals));
        Assert.Contains("Σ brutto: 80.00 kg", Normalize(totals));
        Assert.Contains("Σ tara: 2.30 kg", Normalize(totals));
        Assert.Contains("Σ netto: 77.70 kg", Normalize(totals));
    }

    [Fact]
    public void Commit_ReturnsAllFilledRows_AndResetsGrid()
    {
        var received = new List<List<BucketEntry>>();
        var cut = Render(received);
        Brutto(cut, 0).Input("5");
        Brutto(cut, 0).KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Brutto(cut, 1).Input("25");

        cut.Find("[data-testid=bucket-add]").Click();

        var entries = Assert.Single(received);
        Assert.Equal(new[] { 5m, 25m }, entries.Select(e => e.Gross));
        Assert.Equal(new[] { 0.40m, 0.80m }, entries.Select(e => e.Tare));
        Assert.All(entries, e => Assert.False(e.TareManual));
        Assert.Single(cut.FindAll("[data-testid=bucket-row]"));
        Assert.Equal("", Value(Brutto(cut, 0)));
    }

    [Fact]
    public void NonHoney_UsesPackageTare_AndNeverMarksManual()
    {
        var received = new List<List<BucketEntry>>();
        var cut = Render(received, isHoney: false);

        Brutto(cut, 0).Input("3");
        Assert.Equal("0.05", Value(Tare(cut, 0)));

        Tare(cut, 0).Input("1");
        Assert.Empty(cut.FindAll("[data-testid=tare-manual-marker]"));

        cut.Find("[data-testid=bucket-add]").Click();
        var entry = Assert.Single(Assert.Single(received));
        Assert.False(entry.TareManual);
        Assert.Equal(1m, entry.Tare);
    }

    private static string Normalize(string s) => string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Replace(',', '.');
}
