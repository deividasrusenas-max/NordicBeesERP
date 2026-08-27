using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

/// <summary>
/// Localized label set for the "Prekių pardavimo suvestinė" (Sales by Customer) report,
/// shared by SalesByCustomerPdfService and SalesByCustomerXlsxService so the two renderers
/// never drift. Underlying report data is language-independent; only these strings change.
/// </summary>
public static class SalesByCustomerReportLabels
{
    public class Set
    {
        public string Title { get; init; } = "";
        public string Customer { get; init; } = "";
        public string Product { get; init; } = "";
        public string InvoiceNo { get; init; } = "";
        public string Date { get; init; } = "";
        public string Quantity { get; init; } = "";
        public string UnitPrice { get; init; } = "";
        public string Amount { get; init; } = "";
        public string ProductSubtotal { get; init; } = "";
        public string CustomerTotal { get; init; } = "";
        public string GrandTotal { get; init; } = "";
        public string ProductTotalsSection { get; init; } = "";
        public string NoProduct { get; init; } = "";
        public string AmountInWords { get; init; } = "";
        public string SheetName { get; init; } = "";
        public string AllCustomers { get; init; } = "";
    }

    private static readonly Set Lt = new()
    {
        Title = "PREKIŲ PARDAVIMO SUVESTINĖ",
        Customer = "Klientas",
        Product = "Prekė",
        InvoiceNo = "Sąskaita Nr.",
        Date = "Data",
        Quantity = "Kiekis",
        UnitPrice = "Vnt. kaina",
        Amount = "Suma",
        ProductSubtotal = "Iš viso pagal prekę",
        CustomerTotal = "Iš viso pagal klientą",
        GrandTotal = "Bendra suvestinė",
        ProductTotalsSection = "Pagal prekę (visi klientai)",
        NoProduct = "Nenurodyta prekė",
        AmountInWords = "Suma žodžiais:",
        SheetName = "Pardavimo suvestinė",
        AllCustomers = "Visi klientai",
    };

    private static readonly Set En = new()
    {
        Title = "SALES BY CUSTOMER SUMMARY",
        Customer = "Customer",
        Product = "Product",
        InvoiceNo = "Invoice No.",
        Date = "Date",
        Quantity = "Qty",
        UnitPrice = "Unit price",
        Amount = "Amount",
        ProductSubtotal = "Total by product",
        CustomerTotal = "Total by customer",
        GrandTotal = "Grand total",
        ProductTotalsSection = "By product (all customers)",
        NoProduct = "Unspecified product",
        AmountInWords = "Amount in words:",
        SheetName = "Sales summary",
        AllCustomers = "All customers",
    };

    public static Set For(ReportLanguage lang) => lang == ReportLanguage.EN ? En : Lt;
}
