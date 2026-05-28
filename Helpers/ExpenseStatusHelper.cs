using MudBlazor;

namespace NordicBeesERP.Helpers;

public static class ExpenseStatusHelper
{
    public static string GetLabel(string? status) => status switch
    {
        "PENDING"           => "Laukia apmokėjimo",
        "PENDING_SUPPLIER"  => "Nežinomas tiekėjas",
        "NEEDS_REVIEW"      => "Reikia patikrinti",
        "DUPLICATE_PENDING" => "Dublikatas",
        "REJECTED"          => "Atmesta",
        "PARTIAL"           => "Dalinai apmokėta",
        "PAID"              => "Apmokėta",
        _                   => status ?? "Nežinoma"
    };

    public static Color GetColor(string? status) => status switch
    {
        "PENDING"           => Color.Warning,
        "PENDING_SUPPLIER"  => Color.Error,
        "NEEDS_REVIEW"      => Color.Warning,
        "DUPLICATE_PENDING" => Color.Error,
        "REJECTED"          => Color.Dark,
        "PARTIAL"           => Color.Info,
        "PAID"              => Color.Success,
        _                   => Color.Default
    };

    public static List<string> ParseFlags(string? ocrFlags)
    {
        if (string.IsNullOrWhiteSpace(ocrFlags)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ocrFlags) ?? new(); }
        catch { return new(); }
    }

    public static string GetFlagLabel(string flag) => flag switch
    {
        "VENDOR_NOT_FOUND"   => "Nežinomas tiekėjas",
        "WRONG_RECIPIENT"    => "Ne MB Lakštenai",
        "OWN_COMPANY"        => "Savos įmonės sąskaita",
        "MISSING_AMOUNT"     => "Trūksta sumos",
        "MISSING_INV_NUMBER" => "Trūksta numerio",
        "MISSING_DUE_DATE"   => "Trūksta termino",
        "ZERO_VAT"           => "PVM = 0%",
        "LINES_NOT_FOUND"    => "Eilutės nerastos",
        "AMOUNT_MISMATCH"    => "Sumos nesutampa",
        "LOW_CONFIDENCE"     => "Žemas tikslumas",
        "DUPLICATE"          => "Dublikatas",
        "VIES_UNAVAILABLE"   => "VIES nepasiekiamas",
        "AZURE_LIMIT"        => "Azure limitas viršytas",
        _                    => flag
    };

    public static Color GetFlagColor(string flag) => flag switch
    {
        "VENDOR_NOT_FOUND"   => Color.Error,
        "WRONG_RECIPIENT"    => Color.Error,
        "AMOUNT_MISMATCH"    => Color.Error,
        "DUPLICATE"          => Color.Error,
        "AZURE_LIMIT"        => Color.Error,
        "OWN_COMPANY"        => Color.Warning,
        "MISSING_AMOUNT"     => Color.Warning,
        "MISSING_INV_NUMBER" => Color.Warning,
        "ZERO_VAT"           => Color.Warning,
        "LOW_CONFIDENCE"     => Color.Warning,
        "MISSING_DUE_DATE"   => Color.Default,
        "LINES_NOT_FOUND"    => Color.Default,
        "VIES_UNAVAILABLE"   => Color.Default,
        _                    => Color.Default
    };

    public static bool NeedsAttention(string? status, string? ocrFlags = null, DateTime? dueDate = null) {
        if (status is "PENDING_SUPPLIER" or "NEEDS_REVIEW" or "DUPLICATE_PENDING") return true;
        if (status == "PAID" || status == "REJECTED") return false;
        if (ocrFlags != null && ParseFlags(ocrFlags).Any(IsCriticalFlag)) return true;
        if (dueDate.HasValue && dueDate.Value < DateTime.Today && status != "PAID") return true;
        return false;
    }

    public static bool IsCriticalFlag(string flag) =>
        flag is "VENDOR_NOT_FOUND" or "WRONG_RECIPIENT" or "AMOUNT_MISMATCH" or "DUPLICATE";

    public static string Recalculate(decimal paidAmount, decimal invoiceAmount, DateTime? dueDate, string? currentStatus = null)
    {
        // Jei jau atmesta ar dublikatas — nekeičiame statuso
        if (currentStatus is "REJECTED" or "DUPLICATE_PENDING" or "PENDING_SUPPLIER")
            return currentStatus;

        if (paidAmount >= invoiceAmount && invoiceAmount > 0)
            return "PAID";

        if (paidAmount > 0)
            return "PARTIAL";

        if (currentStatus is "PAID" or "PARTIAL")
            return "PENDING";

        if (dueDate.HasValue && dueDate.Value < DateTime.Today && currentStatus is null or "PENDING")
            return "OVERDUE";

        return currentStatus ?? "PENDING";
    }
}