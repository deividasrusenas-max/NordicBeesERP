namespace NordicBeesERP.Helpers;

public static class LithuanianTimeHelper
{
    private static readonly TimeZoneInfo _lithuanianTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius");

    public static DateTime ToLithuanianTime(DateTime utcDateTime) => TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _lithuanianTz);

    public static string ToLithuanianTimeString(this DateTime utcDateTime) =>
        ToLithuanianTime(utcDateTime).ToString("yyyy-MM-dd HH:mm");
}
