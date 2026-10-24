namespace NordicBeesERP.Helpers;

/// <summary>One wording for due dates on every page (no inverted +/- signs).</summary>
public static class DueDaysText
{
    /// <summary>daysUntilDue = due date − today. Overdue → "vėluoja 9 d.", not yet due → "liko 29 d.", today → "šiandien".</summary>
    public static string UntilDue(int daysUntilDue) =>
        daysUntilDue < 0 ? $"vėluoja {-daysUntilDue} d."
        : daysUntilDue == 0 ? "šiandien"
        : $"liko {daysUntilDue} d.";

    /// <summary>daysFromDue = payment date − due date. History: "40 d. anksčiau" / "5 d. vėliau" / "Laiku".</summary>
    public static string PaymentTiming(int daysFromDue) =>
        daysFromDue < 0 ? $"{-daysFromDue} d. anksčiau"
        : daysFromDue == 0 ? "Laiku"
        : $"{daysFromDue} d. vėliau";
}
