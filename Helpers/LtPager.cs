using Microsoft.Extensions.Localization;
using MudBlazor;

namespace NordicBeesERP.Helpers;

/// <summary>lt-LT texts for MudBlazor pagers. MudTablePager takes them as parameters (not via MudLocalizer), so the Razor pagers pass these constants.</summary>
public static class LtPager
{
    public const string RowsPerPage = "Eilučių puslapyje";
    public const string InfoFormat = "{first_item}–{last_item} iš {all_items}";
}

/// <summary>Global lt-LT MudBlazor localizer (data-grid pager and the pager's navigation tooltips). Unknown keys fall back to MudBlazor's English default.</summary>
public sealed class LtMudLocalizer : MudLocalizer
{
    private static readonly Dictionary<string, string> Texts = new()
    {
        ["MudDataGridPager_RowsPerPage"] = LtPager.RowsPerPage,
        ["MudDataGridPager_InfoFormat"] = LtPager.InfoFormat,
        ["MudDataGridPager_AllItems"] = "Visi",
        ["MudDataGridPager_FirstPage"] = "Pirmas puslapis",
        ["MudDataGridPager_PreviousPage"] = "Ankstesnis puslapis",
        ["MudDataGridPager_NextPage"] = "Kitas puslapis",
        ["MudDataGridPager_LastPage"] = "Paskutinis puslapis",
        ["MudTablePager_FirstPage"] = "Pirmas puslapis",
        ["MudTablePager_PreviousPage"] = "Ankstesnis puslapis",
        ["MudTablePager_NextPage"] = "Kitas puslapis",
        ["MudTablePager_LastPage"] = "Paskutinis puslapis",
    };

    public override LocalizedString this[string key] =>
        Texts.TryGetValue(key, out var value) ? new LocalizedString(key, value) : new LocalizedString(key, key, resourceNotFound: true);

    public static string? Lookup(string key) => Texts.TryGetValue(key, out var v) ? v : null;
}
