using NordicBeesERP.Services.Dunning;
using NordicBeesERP.Services.Email;

namespace NordicBeesERP.Tests;

public class DunningSchedulerTests
{
    private static DateTime Utc(string s) => DateTime.SpecifyKind(DateTime.Parse(s, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), DateTimeKind.Utc);

    // 2026-10-14 is a Wednesday; Vilnius is UTC+3 in October before the 25th.
    [Theory]
    [InlineData("2026-10-14T05:59:00Z", false)]  // 08:59 local
    [InlineData("2026-10-14T06:00:00Z", true)]   // 09:00 local
    [InlineData("2026-10-14T06:59:00Z", true)]   // 09:59 local
    [InlineData("2026-10-14T07:00:00Z", false)]  // 10:00 local
    public void Runs_only_inside_the_configured_hour(string utc, bool expected)
    {
        Assert.Equal(expected, DunningScheduler.ShouldRun(Utc(utc), 9, null));
    }

    [Theory]
    [InlineData("2026-10-17T06:30:00Z")] // Saturday
    [InlineData("2026-10-18T06:30:00Z")] // Sunday
    public void Does_not_run_on_weekends(string utc) => Assert.False(DunningScheduler.ShouldRun(Utc(utc), 9, null));

    [Fact]
    public void Does_not_run_twice_on_the_same_local_day()
    {
        Assert.False(DunningScheduler.ShouldRun(Utc("2026-10-14T06:30:00Z"), 9, new DateOnly(2026, 10, 14)));
        Assert.True(DunningScheduler.ShouldRun(Utc("2026-10-15T06:30:00Z"), 9, new DateOnly(2026, 10, 14)));
    }

    [Fact]
    public void Friday_late_evening_utc_that_is_saturday_in_vilnius_does_not_run()
    {
        // Friday 21:30 UTC = Saturday 00:30 local.
        Assert.False(DunningScheduler.ShouldRun(Utc("2026-10-16T21:30:00Z"), 0, null));
    }

    [Theory]
    [InlineData(false, false, EmailMode.Live, false, DunningRunAction.Nothing)]
    [InlineData(true, false, EmailMode.Live, false, DunningRunAction.NotifyOnly)]
    [InlineData(true, false, EmailMode.Disabled, true, DunningRunAction.NotifyOnly)]
    [InlineData(true, true, EmailMode.Live, false, DunningRunAction.AutoSend)]
    [InlineData(true, true, EmailMode.Live, true, DunningRunAction.ComputeOnly)]      // stale data
    [InlineData(true, true, EmailMode.Redirect, false, DunningRunAction.ComputeOnly)] // not Live
    [InlineData(true, true, EmailMode.Disabled, false, DunningRunAction.ComputeOnly)]
    [InlineData(false, true, EmailMode.Live, false, DunningRunAction.Nothing)]
    public void Decide_matrix(bool enabled, bool auto, EmailMode mode, bool stale, DunningRunAction expected)
    {
        var cfg = new DunningConfig { Enabled = enabled, AutoSend = auto };
        Assert.Equal(expected, DunningScheduler.Decide(cfg, mode, stale));
    }
}
