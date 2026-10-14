namespace NordicBeesERP.Services.Dunning;

/// <summary>
/// Business time for dunning (Europe/Vilnius). The container has no TZ and may lack tzdata, so when the OS zone cannot be
/// resolved we fall back to the EU rule by hand (EET = UTC+2, EEST = UTC+3 from the last Sunday of March 01:00 UTC
/// to the last Sunday of October 01:00 UTC).
/// </summary>
public static class VilniusTime
{
    private static readonly TimeZoneInfo? Zone = Resolve();

    private static TimeZoneInfo? Resolve()
    {
        foreach (var id in new[] { "Europe/Vilnius", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return null;
    }

    public static DateTime ToLocal(DateTime utc)
    {
        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (Zone is not null)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, Zone);
        }

        return utc.AddHours(IsSummerTimeUtc(utc) ? 3 : 2);
    }

    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(ToLocal(utcNow));

    public static int Hour(DateTime utcNow) => ToLocal(utcNow).Hour;

    /// <summary>Converts a Vilnius wall-clock moment to UTC (used to schedule the next daily run).</summary>
    public static DateTime ToUtc(DateTime vilniusLocal)
    {
        vilniusLocal = DateTime.SpecifyKind(vilniusLocal, DateTimeKind.Unspecified);
        if (Zone is not null)
        {
            return TimeZoneInfo.ConvertTimeToUtc(vilniusLocal, Zone);
        }

        // Try summer offset first; accept it only if it maps back to a summer instant.
        var asSummer = vilniusLocal.AddHours(-3);
        return IsSummerTimeUtc(DateTime.SpecifyKind(asSummer, DateTimeKind.Utc)) ? asSummer : vilniusLocal.AddHours(-2);
    }

    public static bool IsSummerTimeUtc(DateTime utc)
    {
        var start = LastSunday(utc.Year, 3).AddHours(1);
        var end = LastSunday(utc.Year, 10).AddHours(1);
        return utc >= start && utc < end;
    }

    private static DateTime LastSunday(int year, int month)
    {
        var d = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        while (d.DayOfWeek != DayOfWeek.Sunday)
        {
            d = d.AddDays(-1);
        }

        return d;
    }
}
