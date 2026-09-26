using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Overflow handling of the project line rule (PROJ-LINE-QTY-PRICE). The rule's ordinary behaviour
/// is tested in <see cref="En16931TotalsValidatorTests"/> ("Project rule" section).
/// </summary>
public class LineAmountPlausibilityRuleTests
{
    [Fact]
    public void OverflowingMultiplication_OutOfRange_NoException()
    {
        var result = LineAmountPlausibilityRule.Check(new[]
        {
            new LineAmountInput(3, decimal.MaxValue, 2m, 1m)
        });

        var entry = Assert.Single(result.OutOfRangeLines);
        Assert.Equal(3, entry.LineNumber);
        Assert.Contains(LineAmountPlausibilityRule.RuleId, entry.Message);
        Assert.Contains("nepatikrinta", entry.Message);
        Assert.Empty(result.PassedLines);
        Assert.Empty(result.Violations);
        Assert.Empty(result.NotApplicableLines);
    }

    [Fact]
    public void OverflowingSubtraction_OutOfRange_NoException()
    {
        // quantity × price = MaxValue fits; MaxValue − MinValue does not
        var result = LineAmountPlausibilityRule.Check(new[]
        {
            new LineAmountInput(1, decimal.MaxValue, 1m, decimal.MinValue)
        });

        Assert.Equal(1, Assert.Single(result.OutOfRangeLines).LineNumber);
        Assert.Empty(result.PassedLines);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void MinValueLineNet_AbsDoesNotThrow()
    {
        // Math.Abs(decimal.MinValue) is representable; only the difference may overflow
        var result = LineAmountPlausibilityRule.Check(new[]
        {
            new LineAmountInput(1, 1m, decimal.MinValue, decimal.MinValue)
        });

        Assert.Equal(new[] { 1 }, result.PassedLines);
        Assert.Empty(result.OutOfRangeLines);
    }

    [Fact]
    public void OneOverflowingLine_OtherLinesStillEvaluated()
    {
        var result = LineAmountPlausibilityRule.Check(new[]
        {
            new LineAmountInput(1, 3888m, 0.2066m, 803.31m),
            new LineAmountInput(2, decimal.MaxValue, decimal.MaxValue, 1m),
            new LineAmountInput(3, 3m, 0.2066m, 803.31m),
            new LineAmountInput(4, null, 1m, 1m)
        });

        Assert.Equal(new[] { 1 }, result.PassedLines);
        Assert.Equal(2, Assert.Single(result.OutOfRangeLines).LineNumber);
        Assert.Equal(3, Assert.Single(result.Violations).LineNumber);
        Assert.Equal(new[] { 4 }, result.NotApplicableLines);
    }

    [Fact]
    public void ExtremeInputs_NeverThrow_EveryLineInExactlyOneList()
    {
        decimal?[] pool =
        {
            null, 0m, 1m, -1m, 0.01m, -0.01m, 0.2066m, 3888m, 803.31m, 1e10m, -1e10m, 1e14m, 7.9e14m,
            1e20m, -1e20m, 7.9e27m, -7.9e27m, decimal.MaxValue, decimal.MinValue,
            0.0000000000000000000000000001m, 12345678901234567890.123456789m
        };
        var random = new Random(20260926);

        for (int i = 0; i < 20_000; i++)
        {
            var lines = Enumerable.Range(1, random.Next(1, 6))
                .Select(n => new LineAmountInput(n,
                    pool[random.Next(pool.Length)], pool[random.Next(pool.Length)], pool[random.Next(pool.Length)]))
                .ToList();

            var result = LineAmountPlausibilityRule.Check(lines);

            var seen = result.PassedLines
                .Concat(result.Violations.Select(v => v.LineNumber))
                .Concat(result.NotApplicableLines)
                .Concat(result.OutOfRangeLines.Select(o => o.LineNumber))
                .OrderBy(n => n)
                .ToList();
            Assert.Equal(lines.Select(l => l.LineNumber).OrderBy(n => n).ToList(), seen);
        }
    }
}
