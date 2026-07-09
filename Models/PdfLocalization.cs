using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace NordicBeesERP.Models;

public static class PdfLocalization
{
    public const string DocumentTitle = "Sąskaita faktūra";

    public const string InvoiceNumberLabel = "Sąskaitos numeris";
    public const string InvoiceDateLabel = "Sąskaitos data";
    public const string PaymentTermLabel = "Mokėjimo terminas";
    public const string PaymentDueDateLabel = "Mokėjimo terminas";
    
    public const string BillToLabel = "Mokėtojas";
    public const string ShipToLabel = "Pristatymo adresas";
    
    public const string ProductDescriptionLabel = "Aprašymas";
    public const string QuantityLabel = "Kiekis";
    public const string UnitPriceLabel = "Kaina";
    public const string VatRateLabel = "PVM %";
    public const string AmountLabel = "Suma";
    
    public const string SubtotalLabel = "Subtotal";
    public const string VatLabel = "PVM";
    public const string TotalLabel = "Iš viso";
    public const string PaidLabel = "Išmokėta";
    public const string DueLabel = "Liko mokėti";
    
    public const string PaymentDetailsLabel = "Mokėjimo informacija";
    public const string BankNameLabel = "Bankas";
    public const string AccountNumberLabel = "Sąskaitos numeris";
    public const string ReferenceLabel = "Mokėjimo paskirtis";
    
    public const string NotesLabel = "Pastabos";
    
    public const string PageLabel = "Puslapis";
    public const string OfLabel = "iš";
    
    public const string CurrencyCode = "EUR";
    public const string CountryLt = "Lietuva";
    public const string CountryEn = "Lithuania";
    
    public const string InvoiceTypeInvoice = "Sąskaita faktūra";
    public const string InvoiceTypeProforma = "Proforma sąskaita";
    public const string InvoiceTypeCredit = "Kreditinė sąskaita";
}
