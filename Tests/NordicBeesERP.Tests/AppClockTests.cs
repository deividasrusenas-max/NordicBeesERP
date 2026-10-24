using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>CLOCK-01: business "today" is the Vilnius date, not the container's UTC date.</summary>
public class AppClockTests
{
    private static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind), DateTimeKind.Utc);

    [Fact]
    public void TodayLt_At2230UtcOnOct23_IsOct24_InVilniusSummerTime()
    {
        // 2026-10-23 22:30 UTC = 2026-10-24 01:30 EEST (DST ends 2026-10-25)
        Assert.Equal(new DateTime(2026, 10, 24), AppClock.TodayLtAt(Utc("2026-10-23T22:30:00Z")));
    }

    [Fact]
    public void TodayLt_AtWinterOffset_UsesPlusTwo()
    {
        // 2026-12-10 22:30 UTC = 2026-12-11 00:30 EET
        Assert.Equal(new DateTime(2026, 12, 11), AppClock.TodayLtAt(Utc("2026-12-10T22:30:00Z")));
        Assert.Equal(new DateTime(2026, 12, 10), AppClock.TodayLtAt(Utc("2026-12-10T21:30:00Z")));
    }

    [Fact]
    public void DaysOverdue_InvoiceDueOct23_At2230UtcOct23_IsOneDay()
    {
        Assert.Equal(1, AppClock.DaysOverdueAt(new DateTime(2026, 10, 23), Utc("2026-10-23T22:30:00Z")));
    }

    [Fact]
    public void DaysOverdue_NotYetDue_IsZero()
    {
        Assert.Equal(0, AppClock.DaysOverdueAt(new DateTime(2026, 10, 24), Utc("2026-10-23T22:30:00Z")));
        Assert.Equal(0, AppClock.DaysOverdueAt(new DateTime(2026, 10, 25), Utc("2026-10-23T22:30:00Z")));
    }

    [Fact]
    public void OverdueFilter_UsesSuppliedToday_DueOct23IsOverdueOnVilniusOct24()
    {
        var today = AppClock.TodayLtAt(Utc("2026-10-23T22:30:00Z"));
        Assert.True(new DateTime(2026, 10, 23) < today);
    }
}
