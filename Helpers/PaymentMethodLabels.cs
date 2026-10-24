namespace NordicBeesERP.Helpers;

/// <summary>
/// ONE Lithuanian label for a payment method, for both vocabularies in the app: customer payments
/// (bank_transfer / cash / card / other / offset, or the C# enum names BankTransfer / Cash / Card / Other / Offset)
/// and expense/supplier payments (BANK / CASH / OTHER / OFFSET). A settlement-act payment shows its act number.
/// </summary>
public static class PaymentMethodLabels
{
    public static string Label(string? method, string? reference = null)
    {
        var key = (method ?? string.Empty).Trim().Replace("_", "").ToLowerInvariant();
        switch (key)
        {
            case "banktransfer":
            case "bank":
                return "Banko pavedimas";
            case "cash":
                return "Grynais";
            case "card":
                return "Kortelė";
            case "electronicmoney":
                return "Elektroniniai pinigai";
            case "other":
                return "Kita";
            case "offset":
                return string.IsNullOrWhiteSpace(reference) ? "Užskaita" : $"Užskaita {reference.Trim()}";
            default:
                return method ?? string.Empty;
        }
    }
}
