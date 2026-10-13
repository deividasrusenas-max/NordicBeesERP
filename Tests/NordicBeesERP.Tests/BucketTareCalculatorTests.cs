using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using System.Globalization;
using Xunit;

namespace NordicBeesERP.Tests;

public class BucketTareCalculatorTests
{
    private static readonly BucketTareCalculator Calc = new();

    private static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);

    private static BucketTareRange R(decimal min, decimal max, decimal tare, int sort, bool active = true, int id = 0)
        => new() { Id = id, MinBruttoKg = min, MaxBruttoKg = max, TareKg = tare, SortOrder = sort, IsActive = active };

    private static List<BucketTareRange> Seed() => new()
    {
        R(1.00m, 16.00m, 0.40m, 1),
        R(16.01m, 40.00m, 0.80m, 2),
        R(40.01m, 55.00m, 1.10m, 3)
    };

    [Theory]
    [InlineData("1.00", "0.40", "0.60")]
    [InlineData("8.50", "0.40", "8.10")]
    [InlineData("16.00", "0.40", "15.60")]
    [InlineData("16.01", "0.80", "15.21")]
    [InlineData("16.10", "0.80", "15.30")]
    [InlineData("25.00", "0.80", "24.20")]
    [InlineData("40.00", "0.80", "39.20")]
    [InlineData("40.01", "1.10", "38.91")]
    [InlineData("40.10", "1.10", "39.00")]
    [InlineData("55.00", "1.10", "53.90")]
    public void InRange_ReturnsTareAndNetto(string brutto, string tare, string netto)
    {
        var result = Calc.GetTare(D(brutto), Seed());

        Assert.False(result.IsOutOfRange);
        Assert.Equal(D(tare), result.Tare);
        Assert.Equal(D(netto), result.Netto);
    }

    [Fact]
    public void Brutto16_005_FallsIntoSecondInterval_NoGap()
    {
        var result = Calc.GetTare(16.005m, Seed());

        Assert.False(result.IsOutOfRange);
        Assert.Equal(0.80m, result.Tare);
        Assert.Equal(15.21m, result.Netto); // 15.205 rounds away from zero
    }

    [Theory]
    [InlineData("0.99")]
    [InlineData("55.01")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("-0.01")]
    [InlineData("1000")]
    public void OutOfRange_ReturnsNullTareAndNetto(string brutto)
    {
        var result = Calc.GetTare(D(brutto), Seed());

        Assert.True(result.IsOutOfRange);
        Assert.Null(result.Tare);
        Assert.Null(result.Netto);
    }

    [Fact]
    public void InactiveInterval_IsIgnored()
    {
        var ranges = Seed();
        ranges[1].IsActive = false; // 16.01–40.00 disabled

        Assert.True(Calc.GetTare(25m, ranges).IsOutOfRange);
        Assert.Equal(0.40m, Calc.GetTare(10m, ranges).Tare);
        Assert.Equal(1.10m, Calc.GetTare(50m, ranges).Tare);
    }

    [Fact]
    public void ChangedTare_ProducesNewResult()
    {
        var ranges = Seed();
        ranges[1].TareKg = 0.50m;

        var result = Calc.GetTare(25m, ranges);

        Assert.Equal(0.50m, result.Tare);
        Assert.Equal(24.50m, result.Netto);
    }

    [Fact]
    public void EmptyRanges_AreOutOfRange()
    {
        Assert.True(Calc.GetTare(10m, new List<BucketTareRange>()).IsOutOfRange);
    }

    [Fact]
    public void Ranges_AreOrderedBySortOrder_NotListOrder()
    {
        var shuffled = new List<BucketTareRange>
        {
            R(40.01m, 55.00m, 1.10m, 3),
            R(1.00m, 16.00m, 0.40m, 1),
            R(16.01m, 40.00m, 0.80m, 2)
        };

        Assert.Equal(0.80m, Calc.GetTare(30m, shuffled).Tare);
        Assert.Equal(0.40m, Calc.GetTare(16m, shuffled).Tare);
    }

    [Theory]
    [InlineData("25.123", "0.8", "24.32")]
    [InlineData("10.005", "0.4", "9.61")]
    public void Netto_RoundsToTwoDecimals_AwayFromZero(string brutto, string tare, string expected)
    {
        Assert.Equal(D(expected), BucketTareCalculator.Netto(D(brutto), D(tare)));
    }
}

public class BucketTareRangeValidatorTests
{
    private static decimal D(string v) => decimal.Parse(v, CultureInfo.InvariantCulture);

    private static BucketTareRange R(decimal min, decimal max, decimal tare, bool active = true)
        => new() { MinBruttoKg = min, MaxBruttoKg = max, TareKg = tare, IsActive = active };

    [Fact]
    public void ValidSet_IsAccepted()
    {
        var errors = BucketTareRangeValidator.Validate(new[]
        {
            R(1.00m, 16.00m, 0.40m), R(16.01m, 40.00m, 0.80m), R(40.01m, 55.00m, 1.10m)
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void OverlappingIntervals_AreRejected()
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(1m, 20m, 0.4m), R(16m, 40m, 0.8m) });

        Assert.Contains(errors, e => e.Contains("persidengia"));
    }

    [Fact]
    public void TouchingBoundary_CountsAsOverlap()
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(1m, 16m, 0.4m), R(16m, 40m, 0.8m) });

        Assert.Contains(errors, e => e.Contains("persidengia"));
    }

    [Theory]
    [InlineData("16", "16")]
    [InlineData("20", "16")]
    public void MinNotBelowMax_IsRejected(string min, string max)
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(D(min), D(max), 0.4m) });

        Assert.Contains(errors, e => e.Contains("apatinė riba"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.4")]
    public void TareNotPositive_IsRejected(string tare)
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(1m, 16m, D(tare)) });

        Assert.Contains(errors, e => e.Contains("didesnė už 0"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public void TareNotBelowMin_IsRejected(string tare)
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(1m, 16m, D(tare)) });

        Assert.Contains(errors, e => e.Contains("mažesnė už apatinę ribą"));
    }

    [Fact]
    public void AllInactive_IsRejected()
    {
        var errors = BucketTareRangeValidator.Validate(new[] { R(1m, 16m, 0.4m, active: false) });

        Assert.Contains(errors, e => e.Contains("bent vienas aktyvus"));
    }

    [Fact]
    public void EmptySet_IsRejected()
    {
        Assert.NotEmpty(BucketTareRangeValidator.Validate(Array.Empty<BucketTareRange>()));
    }

    [Fact]
    public void InactiveOverlap_IsIgnored()
    {
        var errors = BucketTareRangeValidator.Validate(new[]
        {
            R(1m, 16m, 0.4m), R(10m, 30m, 0.8m, active: false)
        });

        Assert.Empty(errors);
    }
}
