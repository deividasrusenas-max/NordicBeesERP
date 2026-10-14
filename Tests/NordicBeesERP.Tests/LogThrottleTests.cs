using NordicBeesERP.Services.Email;
using Xunit;

namespace NordicBeesERP.Tests;

public class LogThrottleTests
{
    [Fact]
    public void First_call_logs_then_suppressed_until_interval_elapsed()
    {
        var t = new LogThrottle(TimeSpan.FromHours(1));
        var t0 = new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc);
        Assert.True(t.ShouldLog("a", t0));
        Assert.False(t.ShouldLog("a", t0.AddSeconds(30)));
        Assert.False(t.ShouldLog("a", t0.AddMinutes(59)));
        Assert.True(t.ShouldLog("a", t0.AddMinutes(60)));
        Assert.False(t.ShouldLog("a", t0.AddMinutes(61)));
    }

    [Fact]
    public void Different_reasons_are_throttled_independently()
    {
        var t = new LogThrottle(TimeSpan.FromHours(1));
        var t0 = DateTime.UtcNow;
        Assert.True(t.ShouldLog("disabled", t0));
        Assert.True(t.ShouldLog("smtp", t0.AddSeconds(30)));
        Assert.False(t.ShouldLog("disabled", t0.AddSeconds(31)));
    }
}
