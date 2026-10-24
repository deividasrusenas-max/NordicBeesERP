namespace NordicBeesERP.Helpers;

/// <summary>
/// Business "today" / "now" in Europe/Vilnius. Containers run in UTC, so between 00:00 and 03:00 Vilnius
/// <c>AppClock.TodayLt</c> is still yesterday. Use this for BUSINESS dates (default dates in dialogs, overdue
/// and due-date comparisons, report periods). Do NOT use it for stored timestamps (created_at, updated_at):
/// those stay <c>DateTime.UtcNow</c>.
/// </summary>
public static class AppClock
{
    private static readonly TimeZoneInfo VilniusTz = Resolve();

    private static TimeZoneInfo Resolve()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time"); }
    }

    /// <summary>Vilnius wall-clock for the given UTC instant (test seam: pass a fixed instant).</summary>
    public static DateTime NowLtAt(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VilniusTz);

    /// <summary>Vilnius calendar date for the given UTC instant.</summary>
    public static DateTime TodayLtAt(DateTime utc) => NowLtAt(utc).Date;

    public static DateTime NowLt => NowLtAt(DateTime.UtcNow);

    public static DateTime TodayLt => TodayLtAt(DateTime.UtcNow);

    /// <summary>Whole days past the due date as seen in Vilnius at the given UTC instant (0 when not yet due).</summary>
    public static int DaysOverdueAt(DateTime dueDate, DateTime utc) =>
        Math.Max(0, (TodayLtAt(utc) - dueDate.Date).Days);
}
