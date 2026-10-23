using MudBlazor;
using NordicBeesERP.Models.WarehouseModule;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Single vocabulary (label + colour) for deliveries.status, shared by the delivery
/// lists and detail windows. DB values are unchanged.
/// </summary>
public static class DeliveryStatusHelper
{
    public static readonly IReadOnlyDictionary<string, (string Label, Color Color)> Map = new Dictionary<string, (string Label, Color Color)>
    {
        { "RECEIVED", ("Priimtas", Color.Default) },
        { "PRICED", ("Neapmokėtas", Color.Error) },
        { "PARTIAL_PAID", ("Dalinai apmokėtas", Color.Warning) },
        { "PAID", ("Apmokėtas", Color.Success) }
    };

    public static string LabelForKey(string? status) => StatusDisplayHelper.GetLabel(status ?? "RECEIVED", Map);

    public static Color ColorForKey(string? status) => StatusDisplayHelper.GetColor(status ?? "RECEIVED", Map);

    /// <summary>
    /// Effective status shown in every delivery window: derived from DebtBase / PaidAmount,
    /// not from the stored deliveries.status (which can lag behind payments and pricing).
    /// </summary>
    public static string KeyFor(Delivery d) => KeyFromAmounts(d.DebtBase, d.PaidAmount);

    public static string GetLabel(Delivery d) => LabelForKey(KeyFor(d));

    public static Color GetColor(Delivery d) => ColorForKey(KeyFor(d));

    /// <summary>Status key derived from amounts (the pricing list recomputes it from debt and payments).</summary>
    public static string KeyFromAmounts(decimal debtBase, decimal paidAmount) =>
        debtBase <= 0 ? "RECEIVED"
        : paidAmount <= 0 ? "PRICED"
        : paidAmount >= debtBase ? "PAID"
        : "PARTIAL_PAID";
}
