namespace NordicBeesERP.Helpers;

public static class InvoiceTypes
{
    public const string Standard        = "PVM SĄSKAITA FAKTŪRA";
    public const string Ulak6           = "6% PVM SĄSKAITA FAKTŪRA";
    public const string ReverseCharge96 = "PVM SĄSKAITA FAKTŪRA (96 str.)";

    public static bool IsReverseCharge96(bool reverseCharge, string? invoiceType) => reverseCharge && !(invoiceType?.Contains("6%") ?? false);
}
