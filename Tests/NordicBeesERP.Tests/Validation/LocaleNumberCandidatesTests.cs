using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

public class LocaleNumberCandidatesTests
{
    private static decimal D(string invariant) =>
        decimal.Parse(invariant, System.Globalization.CultureInfo.InvariantCulture);

    // --- The real ASF0021438 strings (content vs Azure's typed value, raw-asf0021438.json) ---

    [Fact]
    public void Asf0021438_Line1Quantity_ReadAsThree_Misread()
    {
        var result = LocaleNumberCandidates.Check("3 888,000", 3m);

        Assert.Equal(NumberReadOutcome.Misread, result.Outcome);
        Assert.Equal(new[] { 3888m }, result.Candidates);
    }

    [Fact]
    public void Asf0021438_Line2Quantity_ReadAs9000_Ambiguous()
    {
        var result = LocaleNumberCandidates.Check("9,000", 9000m);

        Assert.Equal(NumberReadOutcome.Ambiguous, result.Outcome);
        Assert.Equal(new[] { 9m, 9000m }, result.Candidates);
    }

    [Theory]
    [InlineData("972,00", "972.00")]
    [InlineData("158,40", "158.4")]
    [InlineData("0,2066", "0.2066")]
    [InlineData("14,5456", "14.5456")]
    [InlineData("934,22", "934.22")]
    [InlineData("1 130,40", "1130.4")]
    public void Asf0021438_Amounts_Match(string printed, string azure)
    {
        var result = LocaleNumberCandidates.Check(printed, D(azure));

        Assert.Equal(NumberReadOutcome.Match, result.Outcome);
        Assert.Single(result.Candidates);
    }

    [Fact]
    public void AzureValueThroughDouble_StillMatches()
    {
        Assert.Equal(NumberReadOutcome.Match, LocaleNumberCandidates.Check("0,2066", (decimal)0.2066d).Outcome);
        Assert.Equal(NumberReadOutcome.Match, LocaleNumberCandidates.Check("1 130,40", (decimal)1130.4d).Outcome);
    }

    // --- Candidates ---

    [Theory]
    [InlineData("3 888,000", new[] { "3888" })]
    [InlineData("3 888,000", new[] { "3888" })]      // no-break space
    [InlineData("3 888,000", new[] { "3888" })]      // narrow no-break space
    [InlineData("1.234.567,89", new[] { "1234567.89" })]
    [InlineData("1,234,567.89", new[] { "1234567.89" })]
    [InlineData("1 234 567.89", new[] { "1234567.89" })]
    [InlineData("1.234", new[] { "1.234", "1234" })]
    [InlineData("1,000", new[] { "1", "1000" })]
    [InlineData("1234,5", new[] { "1234.5" })]
    [InlineData("0,123", new[] { "0.123" })]              // "0" cannot open a thousands group
    [InlineData("0.500", new[] { "0.5" })]
    [InlineData("12", new[] { "12" })]
    [InlineData("1 234", new[] { "1234" })]
    [InlineData("007", new[] { "7" })]
    public void Parse_StrictCandidates(string printed, string[] expected)
    {
        Assert.Equal(expected.Select(D).ToArray(), LocaleNumberCandidates.Parse(printed).ToArray());
    }

    // --- Grouping violations and other text with no strict reading ---

    [Theory]
    [InlineData("1,1.1")]
    [InlineData("1.1,1")]
    [InlineData("12,34,567")]      // Indian grouping — not a convention here
    [InlineData("1 23,00")]        // group of 2
    [InlineData("1 2345,00")]      // group of 4
    [InlineData("1.234 567,00")]   // mixed separators
    [InlineData("1,234.567,89")]
    [InlineData("(158,40)")]
    [InlineData("158,40-")]
    [InlineData("--5")]
    [InlineData(",5")]
    [InlineData("€")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NoStrictReading_NotCheckable(string? printed)
    {
        Assert.Empty(LocaleNumberCandidates.Parse(printed));
        Assert.Equal(NumberReadOutcome.NotCheckable, LocaleNumberCandidates.Check(printed, 1m).Outcome);
    }

    // --- Negatives ---

    [Theory]
    [InlineData("-158,40", "-158.40")]
    [InlineData("−158,40", "-158.40")]   // minus sign
    [InlineData("- 1 130,40", "-1130.40")]
    [InlineData("+5,00", "5")]
    public void Negative_Match(string printed, string azure)
    {
        var result = LocaleNumberCandidates.Check(printed, D(azure));

        Assert.Equal(NumberReadOutcome.Match, result.Outcome);
    }

    [Fact]
    public void Negative_SignLostByAzure_Misread()
    {
        Assert.Equal(NumberReadOutcome.Misread, LocaleNumberCandidates.Check("-158,40", 158.40m).Outcome);
    }

    [Fact]
    public void NegativeAmbiguous()
    {
        var result = LocaleNumberCandidates.Check("-9,000", -9m);

        Assert.Equal(NumberReadOutcome.Ambiguous, result.Outcome);
        Assert.Equal(new[] { -9000m, -9m }, result.Candidates);
    }

    // --- Decoration ---

    [Theory]
    [InlineData("1 130,40 €", "1130.40")]
    [InlineData("EUR 1 130,40", "1130.40")]
    [InlineData("21 %", "21")]
    [InlineData("3 888,000 kg", "3888")]
    public void CurrencyUnitsAndPercent_Ignored(string printed, string azure)
    {
        Assert.Equal(NumberReadOutcome.Match, LocaleNumberCandidates.Check(printed, D(azure)).Outcome);
    }

    // --- Outcome rules ---

    [Fact]
    public void AmbiguousText_ValueOutsideCandidates_Misread()
    {
        var result = LocaleNumberCandidates.Check("9,000", 90m);

        Assert.Equal(NumberReadOutcome.Misread, result.Outcome);
        Assert.Equal(new[] { 9m, 9000m }, result.Candidates);
    }

    [Fact]
    public void NoAzureValue_NotCheckable()
    {
        var result = LocaleNumberCandidates.Check("972,00", null);

        Assert.Equal(NumberReadOutcome.NotCheckable, result.Outcome);
        Assert.Equal(new[] { 972m }, result.Candidates);
    }

    [Fact]
    public void RandomInputs_NeverThrow()
    {
        var random = new Random(20260926);
        const string alphabet = "0123456789 .,-+− €%kg()";
        decimal?[] values = { null, 0m, 1m, -1m, 9m, 9000m, 3888m, decimal.MaxValue, decimal.MinValue };
        for (int i = 0; i < 20_000; i++)
        {
            var chars = Enumerable.Range(0, random.Next(0, 40)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
            var result = LocaleNumberCandidates.Check(new string(chars), values[random.Next(values.Length)]);
            Assert.Equal(result.Candidates.Distinct().OrderBy(c => c), result.Candidates);
        }
    }
}
