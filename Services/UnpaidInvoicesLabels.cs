using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// Localized label set for the statement of unpaid invoices (PDF + XLSX).
/// Underlying report data is language-independent; only these strings change
/// between LT and EN. Shared by the PDF and XLSX renderers so the two never drift.
/// </summary>
public static class UnpaidInvoicesLabels
{
    public class Set
    {
        public string Title { get; init; } = "";
        public string ColInvoiceNo { get; init; } = "";
        public string ColDate { get; init; } = "";
        public string ColDueDate { get; init; } = "";
        public string ColAmount { get; init; } = "";
        public string ColBalanceDue { get; init; } = "";
        public string TotalLabel { get; init; } = "";
    }

    private static readonly Set Lt = new()
    {
        Title = "NEAPMOKĖTŲ SĄSKAITŲ SUVESTINĖ",
        ColInvoiceNo = "Sąsk. Nr.",
        ColDate = "Data",
        ColDueDate = "Apmokėjimo terminas",
        ColAmount = "Suma",
        ColBalanceDue = "Neapmokėta suma",
        TotalLabel = "Iš viso",
    };

    private static readonly Set En = new()
    {
        Title = "STATEMENT OF UNPAID INVOICES",
        ColInvoiceNo = "Invoice No",
        ColDate = "Date",
        ColDueDate = "Due Date",
        ColAmount = "Amount",
        ColBalanceDue = "Balance Due",
        TotalLabel = "Total",
    };

    public static Set For(ReportLanguage lang) => lang == ReportLanguage.EN ? En : Lt;
}
