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

    private static IEnumerable<(string File, string Src)> ComponentSources()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root(), "Components"), "*.razor", SearchOption.AllDirectories))
            yield return (Path.GetRelativePath(Root(), file), File.ReadAllText(file));
    }

    [Fact]
    public void MudNumericField_Decimal_AlwaysHasSharedConverterAndLtCulture()
    {
        var offenders = new List<string>();
        foreach (var (file, src) in ComponentSources())
        {
            foreach (Match m in Regex.Matches(src, @"<MudNumericField\b.*?(?<![-=])/?>", RegexOptions.Singleline))
            {
                var t = Regex.Match(m.Value, "\\bT=\"([\\w?]+)\"").Groups[1].Value.TrimEnd('?').ToLowerInvariant();
                if (t == "") { offenders.Add($"{file}: MudNumericField without explicit T"); continue; }
                if (t is "decimal" or "double" or "float"
                    && (!m.Value.Contains("Converter=\"@LtNumberConverters.") || !m.Value.Contains("Culture=\"@LtCulture.Instance\"")))
                    offenders.Add($"{file}: decimal MudNumericField needs Converter=\"@LtNumberConverters.*\" and Culture=\"@LtCulture.Instance\"");
            }
        }
        Assert.Empty(offenders);
    }

    [Fact]
    public void Components_NoNumberInputs_ForDecimalValues()
    {
        var offenders = ComponentSources()
            .Where(x => Regex.IsMatch(x.Src, "type\\s*=\\s*[\"']number[\"']|InputType\\.Number"))
            .Select(x => x.File).ToList();
        Assert.Empty(offenders); // browser-locale dependent: use MudNumericField with the shared converter or a text input + LtNumber
    }

    [Fact]
    public void Components_NoManualDecimalParse_UseLtNumber()
    {
        var offenders = ComponentSources()
            .Where(x => Regex.IsMatch(x.Src, @"\b(decimal|double|float)\.(Try)?Parse\("))
            .Select(x => x.File).ToList();
        Assert.Empty(offenders); // typed numbers: LtNumber.TryParseDecimal; machine formats belong in Helpers/Services
    }
}
