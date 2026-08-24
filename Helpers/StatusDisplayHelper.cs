using MudBlazor;

namespace NordicBeesERP.Helpers;

public static class StatusDisplayHelper
{
    public static readonly IReadOnlyDictionary<string, (string Label, Color Color)> OrderStatusMap = new Dictionary<string, (string Label, Color Color)>
    {
        { "draft", ("Nepakuotas", Color.Default) },
        { "confirmed", ("Patvirtintas", Color.Info) },
        { "packing", ("Pakuojamas", Color.Warning) },
        { "ready_for_pickup", ("Pasiruošęs", Color.Success) },
        { "partially_shipped", ("Dalis išsiųsta", Color.Warning) },
        { "shipped", ("Išsiųstas", Color.Primary) }
    };

    public static string GetLabel(string status, IReadOnlyDictionary<string, (string Label, Color Color)> map) =>
        map.TryGetValue(status, out var entry) ? entry.Label : status;

    public static Color GetColor(string status, IReadOnlyDictionary<string, (string Label, Color Color)> map) =>
        map.TryGetValue(status, out var entry) ? entry.Color : Color.Default;
}
