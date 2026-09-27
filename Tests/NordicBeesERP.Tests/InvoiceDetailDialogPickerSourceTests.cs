using System.Text.RegularExpressions;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 S6 (D-044 Q3): the „Priskirti esamam" and „Pakeisti tiekėją" pickers are ranked by <c>SupplierNameRanker</c> with the
/// matcher's candidates on top, and ranking never assigns. There is no Blazor renderer in this test project, so this guards the markup source.
/// </summary>
public class InvoiceDetailDialogPickerSourceTests
{
    private static string DialogSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NordicBeesERP.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, "Components", "Dialogs", "InvoiceDetailDialog.razor"));
    }

    [Fact]
    public void BothPickers_AreOrderedThroughTheRanker_WithTheMatcherCandidatesOnTop()
    {
        var source = DialogSource();

        Assert.Single(Regex.Matches(source, @"SupplierNameRanker\.Order\("));
        Assert.Contains("candidateIds.Contains(p.Id)", source);                       // matcher candidates are the priority group
        Assert.Contains("RankSuppliers(_suppliers.Where(x => x.Id != _invoice!.SupplierId))", source);   // „Pakeisti tiekėją"
        Assert.Contains("_filteredSuppliers = RankSuppliers(_suppliers).ToList();", source);              // „Priskirti esamam", opened
        Assert.Contains("_filteredSuppliers = RankSuppliers(_suppliers.Where(s =>", source);              // „Priskirti esamam", searched
    }

    [Fact]
    public void Ranking_NeverAssigns_OnlyTheExplicitButtonsCallTheServiceOnce()
    {
        var source = DialogSource();

        Assert.Single(Regex.Matches(source, @"ExpenseService\.AssignSupplierAsync\("));
        Assert.Single(Regex.Matches(source, @"ExpenseService\.ChangeSupplierAsync\("));
        Assert.DoesNotContain("SupplierNameRanker.Order(_suppliers", source.Replace("\n", ""));   // the ranked list is never auto-picked
        Assert.DoesNotContain(".First()", source[source.IndexOf("RankSuppliers(IEnumerable", StringComparison.Ordinal)..][..600]);
    }
}
