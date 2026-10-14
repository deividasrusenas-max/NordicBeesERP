using NordicBeesERP.Services.Email;
using Xunit;

namespace NordicBeesERP.Tests;

public class EmailBodyTextTests
{
    [Fact]
    public void Paragraphs_become_blank_lines_and_br_becomes_newline()
    {
        var (text, fixedHtml) = EmailBodyText.ToPlainText("<p>Gerb. UAB A &amp; B,</p><p>siunčiame.</p><p>Pagarbiai,<br>Bitės<br>+370</p>");
        Assert.Equal("Gerb. UAB A & B,\n\nsiunčiame.\n\nPagarbiai,\nBitės\n+370", text);
        Assert.Equal(string.Empty, fixedHtml);
    }

    [Fact]
    public void Tags_are_stripped_and_entities_decoded()
    {
        var (text, _) = EmailBodyText.ToPlainText("<p>Suma <strong>10&nbsp;€</strong> &lt;test&gt;</p>");
        Assert.Equal("Suma 10 € <test>", text);
    }

    [Fact]
    public void Generated_table_is_kept_as_fixed_block_not_text()
    {
        var table = "<table style=\"x\"><tr><td>LAK1</td></tr></table>";
        var (text, fixedHtml) = EmailBodyText.ToPlainText("<p>Pradžia</p>" + table + "<p>Pabaiga</p>");
        Assert.Equal(table, fixedHtml);
        Assert.DoesNotContain("LAK1", text);
        Assert.Equal("Pradžia\n\nPabaiga", text);
        Assert.EndsWith(table, EmailBodyText.ToHtml(text, fixedHtml));
    }

    [Fact]
    public void ToHtml_encodes_user_text_and_wraps_paragraphs_and_line_breaks()
    {
        var html = EmailBodyText.ToHtml("A <b>&\nB\n\nC", null);
        Assert.Equal("<p>A &lt;b&gt;&amp;<br>B</p><p>C</p>", html);
    }

    [Fact]
    public void Roundtrip_of_simple_template_is_stable()
    {
        var original = "<p>Gerb. X,</p><p>tekstas.</p><p>Pagarbiai,<br>Įmonė</p>";
        var (text, fx) = EmailBodyText.ToPlainText(original);
        Assert.Equal(original, EmailBodyText.ToHtml(text, fx));
    }

    [Fact]
    public void Empty_input_gives_empty_output()
    {
        Assert.Equal(("", ""), EmailBodyText.ToPlainText(""));
        Assert.Equal("", EmailBodyText.ToHtml("  \n ", null));
    }
}
