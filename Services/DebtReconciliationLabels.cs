using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// Localized label set for the debt reconciliation statement (PDF + XLSX).
/// Underlying report data is language-independent; only these strings change
/// between LT and EN. Shared by DebtReconciliationPdfService and
/// DebtReconciliationXlsxService so the two renderers never drift.
/// </summary>
public static class DebtReconciliationLabels
{
    public class Set
    {
        public string Title { get; init; } = "";
        public string DocNo { get; init; } = "";
        public string DocDate { get; init; } = "";
        public string DueDate { get; init; } = "";
        public string Debit { get; init; } = "";
        public string Credit { get; init; } = "";
        public string Balance { get; init; } = "";
        public string OpeningBalance { get; init; } = "";
        public string Total { get; init; } = "";
        public string ClosingBalance { get; init; } = "";
        public string BalanceAsOf { get; init; } = ""; // expects {0} = date
        public string Director { get; init; } = "";
        public string ChiefAccountant { get; init; } = "";
        public string AmountInWords { get; init; } = "";
        public string CompanySeal { get; init; } = "";
        public string SheetName { get; init; } = "";
        public string PartnerLabel { get; init; } = "";
        public string CompanyCodeLabel { get; init; } = "";
        public string AddressLabel { get; init; } = "";
        public string Minus { get; init; } = "";
    }

    private static readonly Set Lt = new()
    {
        Title = "SKOLŲ SUDERINIMO AKTAS",
        DocNo = "Dok. Nr",
        DocDate = "Dok. data",
        DueDate = "Apmokėjimo data",
        Debit = "Debetas",
        Credit = "Kreditas",
        Balance = "Likutis",
        OpeningBalance = "Pradinis likutis",
        Total = "Iš viso",
        ClosingBalance = "Galutinis likutis",
        BalanceAsOf = "Likutis {0}:",
        Director = "Vadovas:",
        ChiefAccountant = "Vyr. finansininkas:",
        AmountInWords = "Suma žodžiais:",
        CompanySeal = "A.V.",
        SheetName = "Suderinimo aktas",
        PartnerLabel = "Klientas",
        CompanyCodeLabel = "Įm. kodas",
        AddressLabel = "Adresas",
        Minus = "minus",
    };

    private static readonly Set En = new()
    {
        Title = "DEBT RECONCILIATION ACT",
        DocNo = "Doc. No",
        DocDate = "Doc. Date",
        DueDate = "Due Date",
        Debit = "Debit",
        Credit = "Credit",
        Balance = "Balance",
        OpeningBalance = "Opening balance",
        Total = "Total",
        ClosingBalance = "Closing balance",
        BalanceAsOf = "Balance as of {0}:",
        Director = "Director:",
        ChiefAccountant = "Chief Accountant:",
        AmountInWords = "Amount in words:",
        CompanySeal = "Company Seal",
        SheetName = "Reconciliation",
        PartnerLabel = "Customer",
        CompanyCodeLabel = "Company code",
        AddressLabel = "Address",
        Minus = "minus",
    };

    public static Set For(ReportLanguage lang) => lang == ReportLanguage.EN ? En : Lt;

    private static readonly string[] LtMonthsGenitive =
        { "sausio", "vasario", "kovo", "balandžio", "gegužės", "birželio", "liepos", "rugpjūčio", "rugsėjo", "spalio", "lapkričio", "gruodžio" };
    private static readonly string[] EnMonthsShort =
        { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

    /// <summary>
    /// Localized period label. The period always starts Jan 1 of <paramref name="year"/>.
    /// If <paramref name="endMonth"/> is null the whole year is covered.
    /// </summary>
    public static string FormatPeriod(ReportLanguage lang, int year, int? endMonth)
    {
        if (endMonth is null or < 1 or > 12)
            return lang == ReportLanguage.EN ? $"Year {year}" : $"{year} m.";

        var fromMonth = lang == ReportLanguage.EN ? EnMonthsShort[0] : LtMonthsGenitive[0];
        var toMonth = lang == ReportLanguage.EN ? EnMonthsShort[endMonth.Value - 1] : LtMonthsGenitive[endMonth.Value - 1];
        return lang == ReportLanguage.EN
            ? $"{fromMonth}–{toMonth} {year}"
            : $"{year} m. {fromMonth}–{toMonth} mėn.";
    }
}
