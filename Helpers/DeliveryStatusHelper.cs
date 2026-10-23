using MudBlazor;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Single vocabulary (label + colour) for deliveries.status, shared by the delivery
/// lists and detail windows. DB values are unchanged.
/// </summary>
public static class DeliveryStatusHelper
{
    public static readonly IReadOnlyDictionary<string, (string Label, Color Color)> Map = new Dictionary<string, (string Label, Color Color)>
    {
        { "RECEIVED", ("Neįkainotas", Color.Default) },
        { "PRICED", ("Neapmokėtas", Color.Error) },
        { "PARTIAL_PAID", ("Dalinai apmokėtas", Color.Warning) },
        { "PAID", ("Apmokėtas", Color.Success) }
    };

    public static string GetLabel(string? status) => StatusDisplayHelper.GetLabel(status ?? "RECEIVED", Map);

    public static Color GetColor(string? status) => StatusDisplayHelper.GetColor(status ?? "RECEIVED", Map);

    /// <summary>Status key derived from amounts (the pricing list recomputes it from debt and payments).</summary>
    public static string KeyFromAmounts(decimal debtBase, decimal paidAmount) =>
        debtBase <= 0 ? "RECEIVED"
        : paidAmount <= 0 ? "PRICED"
        : paidAmount >= debtBase ? "PAID"
        : "PARTIAL_PAID";
}
