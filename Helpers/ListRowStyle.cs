namespace NordicBeesERP.Helpers;

/// <summary>Row background shared by money lists: overdue debt red, due within a week yellow.</summary>
public static class ListRowStyle
{
    public const string Overdue = "background:#fef2f2";
    public const string DueSoon = "background:#fffbeb";

    public static string ForDue(bool isClosed, DateTime? dueDate, bool hasOpenDebt, DateTime today)
    {
        if (isClosed) return "";
        if (dueDate.HasValue && dueDate.Value < today && hasOpenDebt) return Overdue;
        if (dueDate.HasValue && dueDate.Value <= today.AddDays(7)) return DueSoon;
        return "";
    }
}
