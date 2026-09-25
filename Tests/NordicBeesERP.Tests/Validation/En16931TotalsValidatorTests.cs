using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Pure unit tests for En16931TotalsValidator (BR-CO-10/13/15/16, XPath round() semantics) and the
/// separate project rule LineAmountPlausibilityRule. Figures from RESEARCH-2026-09-25 §1 (ASF0021438)
/// and the Artea staging invoice. No DB involvement.
/// </summary>
public class En16931TotalsValidatorTests
{
    // ASF0021438 header as read by prebuilt-invoice: SubTotal 934,22 + TotalTax 196,18 = 1 130,40.
    private static En16931TotalsInput Asf0021438Header(params decimal?[] lineNets) => new()
    {
        LineNetAmounts = lineNets,
        SumOfLineNet = 934.22m,
        TotalWithoutVat = 934.22m,
        VatTotal = 196.18m,
        TotalWithVat = 1130.40m,
        AmountDue = 1130.40m
    };

    [Fact]
    public void Asf0021438Header_BrCo15Passes()
    {
        var result = En16931TotalsValidator.Validate(Asf0021438Header(803.31m, 130.91m));

        Assert.Contains(En16931TotalsValidator.BrCo15, result.PassedRules);
        Assert.DoesNotContain(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo15);
    }

    [Fact]
    public void Asf0021438CorrectLineNet_BrCo10Passes()
    {
        // One line net 803,31 ("Suma be PVM"), the rest sum to 130,91.
        var result = En16931TotalsValidator.Validate(Asf0021438Header(803.31m, 130.91m));

        Assert.Contains(En16931TotalsValidator.BrCo10, result.PassedRules);
        Assert.Empty(result.Violations);
        Assert.Empty(result.NotApplicable);
    }

    [Fact]
    public void Asf0021438GrossTakenAsLineNet_BrCo10FailsWithExpectedAndActual()
    {
        // The mapper took 972,00 ("Suma su PVM") instead of 803,31.
        var result = En16931TotalsValidator.Validate(Asf0021438Header(972.00m, 130.91m));

        var violation = Assert.Single(result.Violations);
        Assert.Equal(En16931TotalsValidator.BrCo10, violation.RuleId);
        Assert.Equal(1102.91m, violation.Expected);
        Assert.Equal(934.22m, violation.Actual);
        Assert.Contains("BR-CO-10 pažeista", violation.Message);
        Assert.Contains("934,22", violation.Message);
        Assert.Contains("1 102,91", violation.Message);
    }

    [Fact]
    public void ArteaStagingInvoice_LineWithZeroHeader_BrCo10Fails()
    {
        var input = new En16931TotalsInput
        {
            LineNetAmounts = new decimal?[] { 105.41m },
            SumOfLineNet = 0.00m,
            TotalWithoutVat = 0.00m,
            VatTotal = 0.00m,
            TotalWithVat = 0.00m,
            AmountDue = 0.00m
        };

        var result = En16931TotalsValidator.Validate(input);

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo10);
        Assert.Equal(105.41m, violation.Expected);
        Assert.Equal(0.00m, violation.Actual);
    }

    [Theory]
    [InlineData("2.345", "2.35")]
    [InlineData("-2.345", "-2.34")]
    [InlineData("2.344", "2.34")]
    [InlineData("-2.346", "-2.35")]
    [InlineData("0.005", "0.01")]
    [InlineData("-0.005", "0.00")]
    public void XPathRound2_HalvesTowardPositiveInfinity(string value, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture),
            En16931TotalsValidator.XPathRound2(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData("1.00", "1.345", "2.35", true)]    //  2.345 rounds up to  2.35
    [InlineData("1.00", "1.345", "2.34", false)]
    [InlineData("-1.00", "-1.345", "-2.34", true)] // -2.345 rounds up to -2.34 (toward +∞)
    [InlineData("-1.00", "-1.345", "-2.35", false)]
    public void BrCo15_RoundingEdgeCaseBothSigns(string withoutVat, string vat, string withVat, bool passes)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            TotalWithoutVat = decimal.Parse(withoutVat, ci),
            VatTotal = decimal.Parse(vat, ci),
            TotalWithVat = decimal.Parse(withVat, ci)
        });

        Assert.Equal(passes, result.PassedRules.Contains(En16931TotalsValidator.BrCo15));
        Assert.Equal(!passes, result.Violations.Any(v => v.RuleId == En16931TotalsValidator.BrCo15));
    }

    [Fact]
    public void VatTotalMissing_BrCo15NotApplicable_NeverPassed()
    {
        var input = Asf0021438Header(803.31m, 130.91m) with { VatTotal = null };

        var result = En16931TotalsValidator.Validate(input);

        var na = Assert.Single(result.NotApplicable);
        Assert.Equal(En16931TotalsValidator.BrCo15, na.RuleId);
        Assert.Equal(new[] { "BT-110" }, na.MissingInputs);
        Assert.DoesNotContain(En16931TotalsValidator.BrCo15, result.PassedRules);
        Assert.DoesNotContain(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo15);
    }

    [Fact]
    public void BrCo15Violation_LithuanianMessageNamesRule()
    {
        var input = Asf0021438Header(803.31m, 130.91m) with { VatTotal = 190.00m };

        var result = En16931TotalsValidator.Validate(input);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("BR-CO-15 pažeista: suma su PVM 1 130,40 ≠ suma be PVM 934,22 + PVM 190,00", violation.Message);
    }

    [Fact]
    public void OptionalAmountsAbsent_DefaultToZero()
    {
        // BT-107/108/113/114 absent -> 0; BR-CO-13 and BR-CO-16 still evaluated.
        var result = En16931TotalsValidator.Validate(Asf0021438Header(803.31m, 130.91m));

        Assert.Contains(En16931TotalsValidator.BrCo13, result.PassedRules);
        Assert.Contains(En16931TotalsValidator.BrCo16, result.PassedRules);
    }

    [Fact]
    public void AllowanceChargePaidRounding_Applied()
    {
        var input = new En16931TotalsInput
        {
            SumOfLineNet = 100.00m,
            AllowanceTotal = 10.00m,
            ChargeTotal = 5.00m,
            TotalWithoutVat = 95.00m,
            TotalWithVat = 114.95m,
            PaidAmount = 50.00m,
            RoundingAmount = 0.05m,
            AmountDue = 65.00m
        };

        var result = En16931TotalsValidator.Validate(input);

        Assert.Contains(En16931TotalsValidator.BrCo13, result.PassedRules);
        Assert.Contains(En16931TotalsValidator.BrCo16, result.PassedRules);
    }

    [Fact]
    public void NothingExtracted_AllRulesNotApplicable()
    {
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput());

        Assert.Empty(result.PassedRules);
        Assert.Empty(result.Violations);
        Assert.Equal(4, result.NotApplicable.Count);
    }

    [Fact]
    public void LineNetMissingOnOneLine_BrCo10NotApplicable()
    {
        var result = En16931TotalsValidator.Validate(Asf0021438Header(803.31m, null));

        Assert.Contains(result.NotApplicable, na => na.RuleId == En16931TotalsValidator.BrCo10 && na.MissingInputs.Contains("BT-131"));
        Assert.DoesNotContain(En16931TotalsValidator.BrCo10, result.PassedRules);
    }

    // --- Schematron-literal rounding: stated side is not rounded (inputs with > 2 decimals) ---
    // Each case below passed under the earlier both-sides-rounded version and fails (or vice versa)
    // under the literal Schematron text — except BrCo13_ZeroAllowancePresent_ComputedSideRounded,
    // which passes under both and guards the case selection.

    [Fact]
    public void BrCo10_StatedSumNotRounded()
    {
        // xs:decimal(BT-106) = round(Σ BT-131 * 100) div 100: 15.004 ≠ 15.00 (both-sides version: 15.00 = 15.00)
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            LineNetAmounts = new decimal?[] { 10.00m, 5.00m },
            SumOfLineNet = 15.004m
        });

        var violation = Assert.Single(result.Violations);
        Assert.Equal(En16931TotalsValidator.BrCo10, violation.RuleId);
        Assert.Equal(15.00m, violation.Expected);
        Assert.Equal(15.004m, violation.Actual);
        Assert.Contains("15,004", violation.Message);
    }

    [Fact]
    public void BrCo13_AllowanceOnly_StatedTotalNotRounded()
    {
        // BT-109 = round((BT-106 - BT-107) * 100) div 100: 90.001 ≠ 90.00
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            SumOfLineNet = 100.00m,
            AllowanceTotal = 10.00m,
            TotalWithoutVat = 90.001m
        });

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo13);
        Assert.Equal(90.00m, violation.Expected);
        Assert.Equal(90.001m, violation.Actual);
    }

    [Fact]
    public void BrCo13_NoAllowanceNoCharge_ComparedWithoutRounding()
    {
        // BT-109 = BT-106, no round(): 100.00 ≠ 100.004 (both-sides version: 100.00 = 100.00)
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            SumOfLineNet = 100.004m,
            TotalWithoutVat = 100.00m
        });

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo13);
        Assert.Equal(100.004m, violation.Expected);
        Assert.Equal(100.00m, violation.Actual);
    }

    [Fact]
    public void BrCo13_ZeroAllowancePresent_ComputedSideRounded()
    {
        // A present BT-107 = 0 selects the rounded case: round(100.004 * 100) div 100 = 100.00 = BT-109
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            SumOfLineNet = 100.004m,
            AllowanceTotal = 0.00m,
            TotalWithoutVat = 100.00m
        });

        Assert.Contains(En16931TotalsValidator.BrCo13, result.PassedRules);
    }

    [Fact]
    public void BrCo15_StatedTotalWithVatNotRounded()
    {
        // BT-112 = round((BT-109 + BT-110) * 100) div 100: 1130.404 ≠ 1130.40
        var input = Asf0021438Header(803.31m, 130.91m) with { TotalWithVat = 1130.404m, AmountDue = null };

        var result = En16931TotalsValidator.Validate(input);

        var violation = Assert.Single(result.Violations);
        Assert.Equal(En16931TotalsValidator.BrCo15, violation.RuleId);
        Assert.Equal(1130.40m, violation.Expected);
        Assert.Equal(1130.404m, violation.Actual);
        Assert.Equal("BR-CO-15 pažeista: suma su PVM 1 130,404 ≠ suma be PVM 934,22 + PVM 196,18", violation.Message);
    }

    [Fact]
    public void BrCo16_PaidOnly_StatedAmountDueNotRounded()
    {
        // BT-115 = round((BT-112 - BT-113) * 100) div 100: 60.004 ≠ 60.00
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            TotalWithVat = 100.00m,
            PaidAmount = 40.00m,
            AmountDue = 60.004m
        });

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo16);
        Assert.Equal(60.00m, violation.Expected);
        Assert.Equal(60.004m, violation.Actual);
    }

    [Fact]
    public void BrCo16_NoPaidNoRounding_ComparedWithoutRounding()
    {
        // BT-115 = BT-112, no round(): 100.00 ≠ 100.004
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            TotalWithVat = 100.004m,
            AmountDue = 100.00m
        });

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo16);
        Assert.Equal(100.004m, violation.Expected);
        Assert.Equal(100.00m, violation.Actual);
    }

    [Fact]
    public void BrCo16_PaidAndRounding_BothSidesRoundedSeparately()
    {
        // round((60.00 - 0.005) * 100) div 100 = 60.00 = round((100.00 - 40.00) * 100) div 100 -> passes.
        // Both-sides version rounded (100.00 - 40.00 + 0.005) = 60.01 ≠ 60.00 and failed.
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            TotalWithVat = 100.00m,
            PaidAmount = 40.00m,
            RoundingAmount = 0.005m,
            AmountDue = 60.00m
        });

        Assert.Contains(En16931TotalsValidator.BrCo16, result.PassedRules);
    }

    [Fact]
    public void BrCo16_RoundingOnly_TotalWithVatNotRounded()
    {
        // round((BT-115 - BT-114) * 100) div 100 = BT-112: 100.00 ≠ 100.004
        var result = En16931TotalsValidator.Validate(new En16931TotalsInput
        {
            TotalWithVat = 100.004m,
            RoundingAmount = 0.00m,
            AmountDue = 100.00m
        });

        var violation = Assert.Single(result.Violations, v => v.RuleId == En16931TotalsValidator.BrCo16);
        Assert.Equal(100.004m, violation.Expected);
        Assert.Equal(100.00m, violation.Actual);
    }

    // --- Overflow: never throws, rule lands in OutOfRange ---

    [Fact]
    public void OverflowingAmounts_EveryRuleOutOfRange_NoException()
    {
        var input = new En16931TotalsInput
        {
            LineNetAmounts = new decimal?[] { decimal.MaxValue, decimal.MaxValue }, // Σ overflows
            SumOfLineNet = decimal.MaxValue,
            ChargeTotal = decimal.MaxValue,                                           // BT-106 + BT-108 overflows
            TotalWithoutVat = 1e27m,
            VatTotal = 0m,                                                             // (1e27 + 0) * 100 overflows
            TotalWithVat = decimal.MaxValue,
            PaidAmount = decimal.MinValue,                                             // BT-112 - BT-113 overflows
            AmountDue = 0m
        };

        var result = En16931TotalsValidator.Validate(input);

        Assert.Empty(result.PassedRules);
        Assert.Empty(result.Violations);
        Assert.Empty(result.NotApplicable);
        Assert.Equal(
            new[] { En16931TotalsValidator.BrCo10, En16931TotalsValidator.BrCo13, En16931TotalsValidator.BrCo15, En16931TotalsValidator.BrCo16 },
            result.OutOfRange.Select(o => o.RuleId));
        Assert.Equal("BR-CO-15 nepatikrinta: sumos per didelės, skaičiavimas viršija leistiną intervalą",
            result.OutOfRange.Single(o => o.RuleId == En16931TotalsValidator.BrCo15).Message);
    }

    [Fact]
    public void ExtremeInputs_NeverThrow_EveryRuleInExactlyOneList()
    {
        decimal?[] pool =
        {
            null, 0m, 0.005m, -0.005m, 100.004m, 1e26m, -1e26m, 7.9e26m, 1e27m, -1e27m,
            decimal.MaxValue, decimal.MinValue, 0.0000000000000000000000000001m
        };
        var random = new Random(20260926);
        decimal? Pick() => pool[random.Next(pool.Length)];

        for (var i = 0; i < 20000; i++)
        {
            var input = new En16931TotalsInput
            {
                LineNetAmounts = new[] { Pick(), Pick() },
                SumOfLineNet = Pick(),
                AllowanceTotal = Pick(),
                ChargeTotal = Pick(),
                TotalWithoutVat = Pick(),
                VatTotal = Pick(),
                TotalWithVat = Pick(),
                PaidAmount = Pick(),
                RoundingAmount = Pick(),
                AmountDue = Pick()
            };

            var result = En16931TotalsValidator.Validate(input);

            var ruleIds = result.PassedRules
                .Concat(result.Violations.Select(v => v.RuleId))
                .Concat(result.NotApplicable.Select(n => n.RuleId))
                .Concat(result.OutOfRange.Select(o => o.RuleId))
                .OrderBy(r => r, StringComparer.Ordinal);
            Assert.Equal(new[] { "BR-CO-10", "BR-CO-13", "BR-CO-15", "BR-CO-16" }, ruleIds);
        }
    }

    // --- Project rule (not EN 16931) ---

    [Fact]
    public void ProjectLineRule_Asf0021438CorrectQuantity_Passes()
    {
        // "3 888,000" kg × 0,2066 ≈ 803,31
        var result = LineAmountPlausibilityRule.Check(new[] { new LineAmountInput(1, 3888m, 0.2066m, 803.31m) });

        Assert.Equal(new[] { 1 }, result.PassedLines);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void ProjectLineRule_QuantityMisparsedAsThree_Fails()
    {
        // "3 888,000" misparsed as 3
        var result = LineAmountPlausibilityRule.Check(new[] { new LineAmountInput(1, 3m, 0.2066m, 803.31m) });

        var violation = Assert.Single(result.Violations);
        Assert.Equal(1, violation.LineNumber);
        Assert.Contains(LineAmountPlausibilityRule.RuleId, violation.Message);
        Assert.Contains("ne EN 16931", violation.Message);
    }

    [Theory]
    [InlineData("1", "1.00", "1.01", true)]      // diff 0.01 = min tolerance
    [InlineData("1", "1.00", "1.02", false)]     // diff 0.02 > 0.01
    [InlineData("1", "1000.00", "1005.00", true)]  // diff 5.00 <= 0.5 % of 1005 = 5.025
    [InlineData("1", "1000.00", "1006.00", false)] // diff 6.00 > 5.03
    public void ProjectLineRule_Tolerance(string qty, string price, string net, bool passes)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var result = LineAmountPlausibilityRule.Check(new[]
        {
            new LineAmountInput(1, decimal.Parse(qty, ci), decimal.Parse(price, ci), decimal.Parse(net, ci))
        });

        Assert.Equal(passes, result.PassedLines.Contains(1));
    }

    [Fact]
    public void ProjectLineRule_MissingInput_NotApplicable()
    {
        var result = LineAmountPlausibilityRule.Check(new[] { new LineAmountInput(7, null, 0.2066m, 803.31m) });

        Assert.Equal(new[] { 7 }, result.NotApplicableLines);
        Assert.Empty(result.PassedLines);
        Assert.Empty(result.Violations);
    }
}
