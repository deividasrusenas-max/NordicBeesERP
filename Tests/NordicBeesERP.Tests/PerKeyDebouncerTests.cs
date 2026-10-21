using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class PerKeyDebouncerTests
{
    [Fact]
    public async Task TwoRowsEditedWithinDelay_BothSaved()
    {
        var saved = new List<int>();
        await using var d = new PerKeyDebouncer();
        d.Schedule(1, () => { lock (saved) saved.Add(1); return Task.CompletedTask; }, delayMs: 50);
        d.Schedule(2, () => { lock (saved) saved.Add(2); return Task.CompletedTask; }, delayMs: 50);

        await Task.Delay(400);

        Assert.Equal(new[] { 1, 2 }, saved.OrderBy(x => x));
    }

    [Fact]
    public async Task SameRowEditedTwice_SavesOnce_WithLatestValue()
    {
        var saved = new List<string>();
        await using var d = new PerKeyDebouncer();
        d.Schedule("a", () => { lock (saved) saved.Add("first"); return Task.CompletedTask; }, delayMs: 100);
        d.Schedule("a", () => { lock (saved) saved.Add("second"); return Task.CompletedTask; }, delayMs: 100);

        await Task.Delay(500);

        Assert.Equal(new[] { "second" }, saved);
    }

    [Fact]
    public async Task Dispose_FlushesPendingSaves_InsteadOfDroppingThem()
    {
        var saved = false;
        var afterSaveRan = false;
        var d = new PerKeyDebouncer();
        d.Schedule(1, () => { saved = true; return Task.CompletedTask; }, () => { afterSaveRan = true; return Task.CompletedTask; }, delayMs: 60_000);

        await d.DisposeAsync();

        Assert.True(saved);
        Assert.False(afterSaveRan); // no UI callback while the page is going away
    }

    [Fact]
    public async Task FailingSave_ReportsError_AndOtherRowsStillSave()
    {
        Exception? error = null;
        var saved = false;
        await using var d = new PerKeyDebouncer(ex => { error = ex; return Task.CompletedTask; });
        d.Schedule(1, () => throw new InvalidOperationException("boom"), delayMs: 30);
        d.Schedule(2, () => { saved = true; return Task.CompletedTask; }, delayMs: 30);

        await Task.Delay(400);

        Assert.NotNull(error);
        Assert.True(saved);
    }
}
