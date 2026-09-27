using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 4 Part C, C2: pure-computation tests for the working-day calculator
/// (PLAN-ETAPAS4.md §4). The holiday list itself is flagged NEPATVIRTINTA in the plan; these
/// tests check the mechanism (weekend/holiday exclusion, Easter calculation), not the list's
/// authority against an official calendar.
/// </summary>
public class LithuanianWorkingDayCalculatorTests
{
    [Theory]
    [InlineData(2026, 9, 26, false)] // Saturday
    [InlineData(2026, 9, 27, false)] // Sunday
    [InlineData(2026, 9, 28, true)]  // Monday
    public void IsWorkingDay_Weekend_IsExcluded(int year, int month, int day, bool expectedWorkingDay)
    {
        var date = new DateTime(year, month, day);
        Assert.Equal(expectedWorkingDay, LithuanianWorkingDayCalculator.IsWorkingDay(date));
    }

    [Theory]
    [InlineData(2027, 1, 1)]   // Naujieji metai
    [InlineData(2027, 2, 16)]  // Vasario 16-oji
    [InlineData(2027, 3, 11)]  // Kovo 11-oji
    [InlineData(2027, 5, 1)]   // Darbo diena
    [InlineData(2027, 6, 24)]  // Joninės
    [InlineData(2027, 7, 6)]   // Valstybės diena
    [InlineData(2027, 8, 15)]  // Žolinė
    [InlineData(2027, 11, 1)]  // Visų šventųjų
    [InlineData(2027, 11, 2)]  // Vėlinės
    [InlineData(2027, 12, 24)] // Kūčios
    [InlineData(2027, 12, 25)] // Kalėdos
    [InlineData(2027, 12, 26)] // Kalėdos II
    public void IsPublicHoliday_FixedDates_AreHolidays(int year, int month, int day)
    {
        Assert.True(LithuanianWorkingDayCalculator.IsPublicHoliday(new DateTime(year, month, day)));
    }

    [Fact]
    public void IsWorkingDay_OrdinaryWeekday_IsWorkingDay()
    {
        // 2027-01-04 is a Monday and not a listed holiday.
        Assert.True(LithuanianWorkingDayCalculator.IsWorkingDay(new DateTime(2027, 1, 4)));
    }

    [Fact]
    public void HolidayOnAWeekend_IsStillExcludedButNotDoubleCounted()
    {
        // 2027-01-01 (Naujieji metai) falls on a Friday in 2027; 2028-01-01 falls on a Saturday —
        // both must be excluded either way (weekend rule and/or holiday rule).
        var newYear2028 = new DateTime(2028, 1, 1);
        Assert.Equal(DayOfWeek.Saturday, newYear2028.DayOfWeek);
        Assert.False(LithuanianWorkingDayCalculator.IsWorkingDay(newYear2028));
    }

    [Theory]
    [InlineData(2024, 3, 31)]  // known Easter Sunday 2024
    [InlineData(2025, 4, 20)]  // known Easter Sunday 2025
    [InlineData(2026, 4, 5)]   // known Easter Sunday 2026
    [InlineData(2027, 3, 28)]  // known Easter Sunday 2027
    [InlineData(2028, 4, 16)]  // known Easter Sunday 2028 — D-047 asked for 2026-2028 coverage
    public void IsPublicHoliday_EasterSundayAndMonday_AreHolidays(int year, int month, int day)
    {
        var easterSunday = new DateTime(year, month, day);
        Assert.True(LithuanianWorkingDayCalculator.IsPublicHoliday(easterSunday));
        Assert.True(LithuanianWorkingDayCalculator.IsPublicHoliday(easterSunday.AddDays(1)));
        Assert.False(LithuanianWorkingDayCalculator.IsPublicHoliday(easterSunday.AddDays(-1)));
        Assert.False(LithuanianWorkingDayCalculator.IsPublicHoliday(easterSunday.AddDays(2)));
    }

    [Fact]
    public void WorkingDaysElapsed_SameDay_IsZero()
    {
        var date = new DateTime(2027, 1, 4);
        Assert.Equal(0, LithuanianWorkingDayCalculator.WorkingDaysElapsed(date, date));
    }

    [Fact]
    public void WorkingDaysElapsed_EarlierAsOf_IsZero()
    {
        var since = new DateTime(2027, 1, 10);
        var asOf = new DateTime(2027, 1, 4);
        Assert.Equal(0, LithuanianWorkingDayCalculator.WorkingDaysElapsed(since, asOf));
    }

    [Fact]
    public void WorkingDaysElapsed_AcrossOneWeekend_SkipsSaturdayAndSunday()
    {
        // Friday 2027-01-08 to Monday 2027-01-11: only Monday counts (1 working day).
        var since = new DateTime(2027, 1, 8);
        var asOf = new DateTime(2027, 1, 11);
        Assert.Equal(1, LithuanianWorkingDayCalculator.WorkingDaysElapsed(since, asOf));
    }

    [Fact]
    public void WorkingDaysElapsed_AcrossAHoliday_SkipsIt()
    {
        // 2026-12-23 (Wed) to 2026-12-29 (Tue): working days in between are 24(Thu,holiday-Kūčios),
        // 25/26(holidays), 28(Mon), 29(Tue) -> only 28 and 29 count = 2.
        var since = new DateTime(2026, 12, 23);
        var asOf = new DateTime(2026, 12, 29);
        Assert.Equal(2, LithuanianWorkingDayCalculator.WorkingDaysElapsed(since, asOf));
    }
}
