using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using NordicBeesERP.Components.Pages.SettingsPages;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>Renders the real settings page against an in-memory interval service (no DB).</summary>
public class BucketTareSettingsTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly InMemoryTareService _service = new();

    public BucketTareSettingsTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton<IBucketTareService>(_service);
        _ctx.RenderComponent<MudBlazor.MudPopoverProvider>();
        _ctx.RenderComponent<MudBlazor.MudSnackbarProvider>();
    }

    public void Dispose() => _ctx.Dispose();

    private sealed class InMemoryTareService : IBucketTareService
    {
        public List<BucketTareRange> Rows { get; } = new()
        {
            new() { Id = 1, MinBruttoKg = 1.00m, MaxBruttoKg = 16.00m, TareKg = 0.40m, SortOrder = 1, IsActive = true },
            new() { Id = 2, MinBruttoKg = 16.01m, MaxBruttoKg = 40.00m, TareKg = 0.80m, SortOrder = 2, IsActive = true },
            new() { Id = 3, MinBruttoKg = 40.01m, MaxBruttoKg = 55.00m, TareKg = 1.10m, SortOrder = 3, IsActive = true }
        };
        public int SaveCalls { get; private set; }
        public IReadOnlyList<BucketTareRange>? LastSaved { get; private set; }

        public Task<IReadOnlyList<BucketTareRange>> GetRangesAsync(bool includeInactive = false)
            => Task.FromResult<IReadOnlyList<BucketTareRange>>(Rows.Select(r => new BucketTareRange
            {
                Id = r.Id, MinBruttoKg = r.MinBruttoKg, MaxBruttoKg = r.MaxBruttoKg, TareKg = r.TareKg,
                SortOrder = r.SortOrder, IsActive = r.IsActive
            }).ToList());

        public Task<TareResult> GetTareAsync(decimal brutto) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> SaveRangesAsync(IReadOnlyList<BucketTareRange> ranges)
        {
            SaveCalls++;
            LastSaved = ranges.ToList();
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    [Fact]
    public void SeedRows_AreShown_WithNoErrors_AndSaveEnabled()
    {
        var cut = _ctx.RenderComponent<BucketTareSettings>();

        Assert.Equal(3, cut.FindAll("tbody tr").Count);
        Assert.Empty(cut.FindAll("[data-testid=tare-errors]"));
        Assert.False(cut.Find("[data-testid=tare-save]").HasAttribute("disabled"));
    }

    [Fact]
    public void OverlappingEdit_ShowsError_AndBlocksSave()
    {
        var cut = _ctx.RenderComponent<BucketTareSettings>();

        // first row "iki" = 20 overlaps the 16.01–40 interval
        var firstMax = cut.FindAll("tbody tr")[0].QuerySelectorAll("input")[1];
        firstMax.Input("20");

        Assert.Contains("persidengia", cut.Find("[data-testid=tare-errors]").TextContent);
        Assert.True(cut.Find("[data-testid=tare-save]").HasAttribute("disabled"));
        Assert.Equal(0, _service.SaveCalls);
    }

    [Fact]
    public void AddInterval_AppendsRowAfterLastActive_AndSaves()
    {
        var cut = _ctx.RenderComponent<BucketTareSettings>();

        cut.Find("[data-testid=tare-add]").Click();
        Assert.Equal(4, cut.FindAll("tbody tr").Count);
        Assert.Empty(cut.FindAll("[data-testid=tare-errors]"));

        cut.Find("[data-testid=tare-save]").Click();

        Assert.Equal(1, _service.SaveCalls);
        var added = _service.LastSaved!.Single(r => r.Id == 0);
        Assert.Equal(55.01m, added.MinBruttoKg);
    }

    [Fact]
    public void RemovingSavedInterval_SoftDisables_RemovingAllBlocksSave()
    {
        var cut = _ctx.RenderComponent<BucketTareSettings>();

        for (var i = 0; i < 3; i++)
            cut.FindAll("tbody tr")[i].QuerySelector("button")!.Click();

        Assert.Equal(3, cut.FindAll("tbody tr").Count); // soft delete keeps the rows
        Assert.Contains("bent vienas aktyvus", cut.Find("[data-testid=tare-errors]").TextContent);
        Assert.True(cut.Find("[data-testid=tare-save]").HasAttribute("disabled"));
    }
}
