namespace NordicBeesERP.Helpers;

/// <summary>
/// Working-day calculation for the Etapas 4 review-queue aging control (PLAN-ETAPAS4.md §4,
/// D-031 criterion 6). The holiday list is written from general knowledge of Lithuanian
/// statutory holidays, NOT verified against an official calendar in this session — same
/// NEPATVIRTINTA-until-confirmed pattern this project already uses for the VAT rate table
/// (D-039) and formulation lists (D-046 OQ-5). The accountant should confirm the list (open
/// question OQ-2) before it governs anything the owner relies on.
/// </summary>
public static class LithuanianWorkingDayCalculator
{
    /// <summary>Fixed-date statutory holidays: month/day pairs, every year.</summary>
    private static readonly (int Month, int Day)[] FixedHolidays =
    {
        (1, 1),   // Naujieji metai
        (2, 16),  // Lietuvos valstybės atkūrimo diena
        (3, 11),  // Lietuvos nepriklausomybės atkūrimo diena
        (5, 1),   // Tarptautinė darbo diena
        (6, 24),  // Joninės
        (7, 6),   // Valstybės (Lietuvos karaliaus Mindaugo karūnavimo) diena
        (8, 15),  // Žolinė
        (11, 1),  // Visų šventųjų diena
        (11, 2),  // Vėlinės
        (12, 24), // Kūčios
        (12, 25), // Kalėdos (1 diena)
        (12, 26)  // Kalėdos (2 diena)
    };

    public static bool IsWorkingDay(DateTime date) => !IsWeekend(date) && !IsPublicHoliday(date.Date);

    public static bool IsWeekend(DateTime date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static bool IsPublicHoliday(DateTime date)
    {
        date = date.Date;

        foreach (var (month, day) in FixedHolidays)
        {
            if (date.Month == month && date.Day == day) return true;
        }

        var easterSunday = GetEasterSunday(date.Year);
        if (date == easterSunday || date == easterSunday.AddDays(1)) return true;

        return false;
    }

    /// <summary>Working days that have elapsed strictly after <paramref name="since"/> up to and
    /// including <paramref name="asOf"/>. Same day (since == asOf) is 0 elapsed working days.</summary>
    public static int WorkingDaysElapsed(DateTime since, DateTime asOf)
    {
        var start = since.Date;
        var end = asOf.Date;
        if (end <= start) return 0;

        var count = 0;
        for (var date = start.AddDays(1); date <= end; date = date.AddDays(1))
        {
            if (IsWorkingDay(date)) count++;
        }
        return count;
    }

    /// <summary>Anonymous Gregorian (Meeus/Jones/Butcher) algorithm — computed, not looked up, so
    /// no yearly maintenance is needed.</summary>
    private static DateTime GetEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
