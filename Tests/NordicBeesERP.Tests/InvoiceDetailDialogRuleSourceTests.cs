using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// The detail dialog must not carry a second, private line-sum check next to the „Sumų patikra" block: the old
/// „Eilučių suma nesutampa" alert compared line GROSS with header gross at 1 cent, so it contradicted BR-CO-10 results
/// (LINE_SUM_ROUNDING is information, D-040). One source of truth = <c>ExpenseService.DescribeValidation</c>.
/// There is no Blazor renderer in this test project, so this guards the markup source.
/// </summary>
public class InvoiceDetailDialogRuleSourceTests
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
    public void NoSeparateLineSumAlert()
    {
        var source = DialogSource();

        Assert.DoesNotContain("LinesMatchHeader", source);
        Assert.DoesNotContain("nesutampa su sąskaitos suma", source);
    }

    [Fact]
    public void RuleBlock_ReadsTheSharedBrCo10Result()
    {
        var source = DialogSource();

        Assert.Contains("Sumų patikra", source);
        Assert.Contains("ExpenseService.DescribeValidation(", source);
    }
}
