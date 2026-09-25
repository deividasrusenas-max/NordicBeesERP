using MudBlazor;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Helpers;

public static class ExpenseStatusHelper
{
    // D-027: DUPLICATE_PENDING and REJECTED are quarantined — never a liability, never in totals,
    // cash flow or export unless a filter asks for that status explicitly.

    /// <summary>
    /// EF-translatable payable filter. Explicit != comparisons on purpose (FROZEN.md §10).
    /// </summary>
    public static IQueryable<ExpenseInvoice> WhereCountsAsPayable(this IQueryable<ExpenseInvoice> query) =>
        query.Where(i => i.Status != "DUPLICATE_PENDING" && i.Status != "REJECTED");

    /// <summary>In-memory check for materialised lists only — never use inside an EF LINQ expression (use WhereCountsAsPayable).</summary>
    public static bool CountsAsPayable(string? status) =>
        status != "DUPLICATE_PENDING" && status != "REJECTED";

    public static string GetLabel(string? status) => status switch
    {
        "PENDING"           => "Laukia apmokėjimo",
        "PENDING_SUPPLIER"  => "Nežinomas tiekėjas",
        "NEEDS_REVIEW"      => "Reikia patikrinti",
        "DUPLICATE_PENDING" => "Dublikatas",
        "REJECTED"          => "Atmesta",
        "PARTIAL"           => "Dalinai apmokėta",
        "PAID"              => "Apmokėta",
        "OVERDUE"           => "Pradelsta",
        _                   => status ?? "Nežinoma"
    };

    public static Color GetColor(string? status) => status switch
    {
        "PENDING"           => Color.Info,
        "PENDING_SUPPLIER"  => Color.Error,
        "NEEDS_REVIEW"      => Color.Warning,
        "DUPLICATE_PENDING" => Color.Error,
        "REJECTED"          => Color.Dark,
        "PARTIAL"           => Color.Info,
        "PAID"              => Color.Success,
        "OVERDUE"           => Color.Error,
        _                   => Color.Default
    };

    public static List<string> ParseFlags(string? ocrFlags)
    {
        if (string.IsNullOrWhiteSpace(ocrFlags)) return new();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(ocrFlags) ?? new(); }
        catch { return new(); }
    }

    /// <summary>D-025: the due date was not on the document and was assumed (invoice date + 30 d.).</summary>
    public static bool IsDueDateAssumed(string? ocrFlags) => ParseFlags(ocrFlags).Contains("MISSING_DUE_DATE");

    public static string GetFlagLabel(string flag) => GetFlagLabel(flag, "MB Lakštenai");

    public static string GetFlagLabel(string flag, string companyName) => flag switch
    {
        "VENDOR_NOT_FOUND"   => "Nežinomas tiekėjas",
        "WRONG_RECIPIENT"    => $"Ne {companyName}",
        "OWN_COMPANY"        => "Savos įmonės sąskaita",
        "MISSING_AMOUNT"     => "Trūksta sumos",
        "MISSING_INV_NUMBER" => "Trūksta numerio",
        "MISSING_DUE_DATE"   => "Terminas numatytas (+30 d.)",
        "ZERO_VAT"           => "PVM = 0%",
        "INVALID_VAT_RATE"   => "Netinkama PVM norma",
        "AMOUNT_ARITHMETIC_MISMATCH" => "Sumos nesutampa (be PVM + PVM ≠ su PVM)",
        "MISSING_MONEY_FIELD"        => "Trūksta sumos duomenų",
        "FUTURE_DATE"        => "Data ateityje",
        "STALE_DATE"         => "Neįtikėtinai sena data",
        "MISSING_INV_DATE"   => "Trūksta sąskaitos datos",
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
        "INVALID_VAT_RATE"   => Color.Warning,
        "AMOUNT_ARITHMETIC_MISMATCH" => Color.Error,
        "MISSING_MONEY_FIELD"        => Color.Warning,
        "FUTURE_DATE"        => Color.Error,
        "STALE_DATE"         => Color.Error,
        "MISSING_INV_DATE"   => Color.Error,
        "LOW_CONFIDENCE"     => Color.Warning,
        "MISSING_DUE_DATE"   => Color.Default,
        "LINES_NOT_FOUND"    => Color.Default,
        "VIES_UNAVAILABLE"   => Color.Default,
        _                    => Color.Default
    };

    public static bool NeedsAttention(string? status, string? ocrFlags = null, DateTime? dueDate = null) {
        if (status is "PENDING_SUPPLIER" or "NEEDS_REVIEW" or "DUPLICATE_PENDING") return true;
        if (status == "REJECTED") return false;
        // A paid invoice is still highlighted when it carries a critical flag (e.g. broken header
        // arithmetic): being paid does not make the data correct.
        if (ocrFlags != null && ParseFlags(ocrFlags).Any(IsCriticalFlag)) return true;
        if (status == "PAID") return false;
        if (dueDate.HasValue && dueDate.Value < DateTime.Today && status != "PAID") return true;
        return false;
    }

    public static bool IsCriticalFlag(string flag) =>
        flag is "VENDOR_NOT_FOUND" or "WRONG_RECIPIENT" or "AMOUNT_MISMATCH" or "DUPLICATE"
            or "AMOUNT_ARITHMETIC_MISMATCH" or "MISSING_MONEY_FIELD"
            or "FUTURE_DATE" or "STALE_DATE" or "MISSING_INV_DATE";

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