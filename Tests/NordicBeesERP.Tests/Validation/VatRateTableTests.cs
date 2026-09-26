using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

public class VatRateTableTests
{
    private static readonly DateTime InTable = new(2026, 9, 26);

    [Fact]
    public void Ro_DayBeforeChange_OldRates()
    {
        var day = new DateTime(2025, 7, 31);

        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", day, 19m).Outcome);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", day, 9m).Outcome);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", day, 5m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check("RO", day, 21m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check("RO", day, 11m).Outcome);
    }

    [Fact]
    public void Ro_ChangeDay_NewRates()
    {
        var day = new DateTime(2025, 8, 1);

        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", day, 21m).Outcome);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", day, 11m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check("RO", day, 19m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check("RO", day, 9m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check("RO", day, 5m).Outcome);
    }

    [Fact]
    public void TimeOfDayIgnored()
    {
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", new DateTime(2025, 7, 31, 23, 59, 59), 19m).Outcome);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("RO", new DateTime(2025, 8, 1, 0, 0, 1), 21m).Outcome);
    }

    [Theory]
    [InlineData("LT", "21")]
    [InlineData("LT", "9")]
    [InlineData("LT", "5")]
    [InlineData("DE", "19")]
    [InlineData("DE", "7")]
    [InlineData("LV", "21")]
    [InlineData("LV", "12")]
    [InlineData("LV", "5")]
    [InlineData("EE", "24")]
    [InlineData("EE", "13")]
    [InlineData("EE", "9")]
    [InlineData("lt", "21.00")]   // case-insensitive country, scale-insensitive rate
    public void ResearchedRates_Allowed(string country, string rate)
    {
        var r = decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check(country, InTable, r).Outcome);
    }

    [Theory]
    [InlineData("LT", "19")]
    [InlineData("LT", "20.99")]
    [InlineData("DE", "21")]
    [InlineData("EE", "22")]
    [InlineData("LV", "17")]   // a blended rate from a mixed-rate invoice
    public void OtherRates_NotAllowed(string country, string rate)
    {
        var r = decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(VatRateCheckOutcome.NotAllowed, VatRateTable.Check(country, InTable, r).Outcome);
    }

    [Theory]
    [InlineData("LT")]
    [InlineData("RO")]
    [InlineData("PL")]
    [InlineData("FR")]
    [InlineData(null)]
    public void ZeroPercent_NeverNotAllowed(string? country)
    {
        foreach (var day in new[] { new DateTime(2020, 1, 1), new DateTime(2025, 7, 31), InTable })
            Assert.Equal(VatRateCheckOutcome.ZeroRate, VatRateTable.Check(country, day, 0m).Outcome);
    }

    [Theory]
    [InlineData("FR")]
    [InlineData("GB")]
    [InlineData("UA")]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void UnknownCountry(string? country)
    {
        Assert.Equal(VatRateCheckOutcome.UnknownCountry, VatRateTable.Check(country, InTable, 21m).Outcome);
    }

    [Fact]
    public void Pl_TodoRow_NoRateData()
    {
        var result = VatRateTable.Check("PL", InTable, 23m);

        Assert.Equal(VatRateCheckOutcome.NoRateData, result.Outcome);
        Assert.Equal(VatRateRowStatus.Todo, result.Row!.Status);
    }

    [Fact]
    public void DateBeforeResearchedRange_NoRateData()
    {
        // LT/DE/LV/EE are researched for 2026 only; earlier dates are not guessed
        Assert.Equal(VatRateCheckOutcome.NoRateData, VatRateTable.Check("LT", new DateTime(2025, 12, 31), 21m).Outcome);
        Assert.Equal(VatRateCheckOutcome.NoRateData, VatRateTable.Check("EE", new DateTime(2025, 6, 30), 24m).Outcome);
        Assert.Equal(VatRateCheckOutcome.Allowed, VatRateTable.Check("LT", new DateTime(2026, 1, 1), 21m).Outcome);
    }

    [Fact]
    public void NothingConfirmedYet()
    {
        Assert.False(VatRateTable.AllRowsConfirmed);
        Assert.Null(VatRateTable.LastVerified);
        Assert.DoesNotContain(VatRateTable.Rows, r => r.Status == VatRateRowStatus.Confirmed);
        Assert.All(VatRateTable.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Source)));
    }

    [Fact]
    public void Table_CoversExactlyTheD038Countries()
    {
        Assert.Equal(new[] { "DE", "EE", "LT", "LV", "PL", "RO" },
            VatRateTable.Rows.Select(r => r.Country).Distinct().OrderBy(c => c).ToArray());
    }

    [Fact]
    public void RowsOfOneCountry_DoNotOverlap()
    {
        foreach (var group in VatRateTable.Rows.GroupBy(r => r.Country))
        {
            var rows = group.ToList();
            for (int i = 0; i < rows.Count; i++)
                for (int j = i + 1; j < rows.Count; j++)
                {
                    var a = rows[i];
                    var b = rows[j];
                    var aFrom = a.ValidFrom ?? DateTime.MinValue;
                    var aTo = a.ValidTo ?? DateTime.MaxValue;
                    var bFrom = b.ValidFrom ?? DateTime.MinValue;
                    var bTo = b.ValidTo ?? DateTime.MaxValue;
                    Assert.True(aTo < bFrom || bTo < aFrom, $"{group.Key} rows overlap");
                }
        }
    }

    [Fact]
    public void ZeroNeverListedAsARate()
    {
        Assert.DoesNotContain(VatRateTable.Rows, r => r.Rates.Contains(0m));
    }
}
