using System.Text.RegularExpressions;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Source-level guards for Razor markup bugs that compile fine but silently do nothing
/// (no Blazor renderer for these pages in this project).
/// </summary>
public class RazorMarkupGuardTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "NordicBeesERP.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void MudAutocomplete_NeverUsesOnValueChanged_ItIsNotAParameter()
    {
        // MudAutocomplete (8.15) has no OnValueChanged: it ends up as an inert HTML attribute and the handler never runs.
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "Components"), "*.razor", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(src, @"<MudAutocomplete\b.*?/>", RegexOptions.Singleline))
                if (m.Value.Contains("OnValueChanged"))
                    offenders.Add(Path.GetFileName(file));
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void MudNumericField_Decimal_AlwaysHasCulture()
    {
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "Components"), "*.razor", SearchOption.AllDirectories))
        {
            var src = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(src, @"<MudNumericField\b.*?(?<![-=])/?>", RegexOptions.Singleline))
            {
                var t = Regex.Match(m.Value, "\\bT=\"([\\w?]+)\"").Groups[1].Value.TrimEnd('?').ToLowerInvariant();
                if ((t == "decimal" || t == "double" || t == "float") && !m.Value.Contains("Culture"))
                    offenders.Add(Path.GetFileName(file));
            }
        }
        Assert.Empty(offenders);
    }
}
