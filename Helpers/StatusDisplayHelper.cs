using MudBlazor;

namespace NordicBeesERP.Helpers;

public static class StatusDisplayHelper
{
    public static string GetLabel(string status, IReadOnlyDictionary<string, (string Label, Color Color)> map) =>
        map.TryGetValue(status, out var entry) ? entry.Label : status;

    public static Color GetColor(string status, IReadOnlyDictionary<string, (string Label, Color Color)> map) =>
        map.TryGetValue(status, out var entry) ? entry.Color : Color.Default;
}
