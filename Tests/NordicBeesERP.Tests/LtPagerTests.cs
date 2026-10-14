using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class LtPagerTests
{
    [Fact]
    public void Texts_are_lithuanian_and_keep_mudblazor_placeholders()
    {
        Assert.Equal("Eilučių puslapyje", LtPager.RowsPerPage);
        Assert.Equal("{first_item}–{last_item} iš {all_items}", LtPager.InfoFormat);
        Assert.Equal("Eilučių puslapyje", new LtMudLocalizer()["MudDataGridPager_RowsPerPage"].Value);
        Assert.Equal("Kitas puslapis", new LtMudLocalizer()["MudTablePager_NextPage"].Value);
    }

    [Fact]
    public void Every_razor_table_pager_uses_the_lt_texts()
    {
        var root = AppContext.BaseDirectory;
        while (root != null && !Directory.Exists(Path.Combine(root, "Components"))) root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        foreach (var f in Directory.EnumerateFiles(Path.Combine(root!, "Components"), "*.razor", SearchOption.AllDirectories))
            foreach (var line in File.ReadLines(f).Where(l => l.Contains("<MudTablePager")))
                Assert.True(line.Contains("LtPager.RowsPerPage") && line.Contains("LtPager.InfoFormat"), $"{f}: {line.Trim()}");
    }
}
