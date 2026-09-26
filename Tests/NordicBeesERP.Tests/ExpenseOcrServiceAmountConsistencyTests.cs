using NordicBeesERP.Services;
using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Header arithmetic (BR-CO-15, D-040: exact per Schematron) and the PLAN-ETAPAS1 §1.4 mapping, through
/// the one helper every path uses (<see cref="ExpenseService.RecomputeValidationFlags"/>). Until S4 this
/// tested <c>ExpenseOcrService.AddAmountConsistencyFlags</c> (flat 0.02 tolerance), which it replaces.
/// </summary>
public class ExpenseOcrServiceAmountConsistencyTests
{
    private static readonly IReadOnlyList<ExpenseService.ValidationLine> NoLines = Array.Empty<ExpenseService.ValidationLine>();

    private static List<string> Flags(decimal excl, decimal vat, decimal incl, IReadOnlyList<ExpenseService.ValidationLine>? lines = null)
    {
        var flags = new List<string>();
        ExpenseService.RecomputeValidationFlags(flags, excl, vat, incl, lines ?? NoLines);
        return flags;
    }

    [Fact]
    public void ReconciledAmounts_NoFlags()
    {
        Assert.Empty(Flags(100m, 21m, 121m));
    }

    [Fact]
    public void OneCentDifference_AddsArithmeticMismatch_D040()
    {
        // before D-040 this was within the flat 0.02 tolerance and gave no flag
        Assert.Contains(OcrFlag.AmountArithmeticMismatch, Flags(100m, 21m, 121.01m));
    }

    [Fact]
    public void DifferenceAboveOldTolerance_AddsArithmeticMismatch()
    {
        var flags = Flags(100m, 21m, 126m);

        Assert.Contains(OcrFlag.AmountArithmeticMismatch, flags);
        Assert.DoesNotContain(OcrFlag.MissingMoneyField, flags);
    }

    [Fact]
    public void MissingIncl_AddsMissingMoneyFieldAndDoesNotCompute()
    {
        var flags = Flags(100m, 21m, 0m);

        Assert.Contains(OcrFlag.MissingMoneyField, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
    }

    [Fact]
    public void MissingExcl_AddsMissingMoneyField()
    {
        var flags = Flags(0m, 21m, 121m);

        Assert.Contains(OcrFlag.MissingMoneyField, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
    }

    [Fact]
    public void ZeroVat_Reconciled_NoFlags()
    {
        Assert.Empty(Flags(100m, 0m, 100m));
    }

    // ---------- PLAN-ETAPAS1 §1.4 mapping ----------

    [Fact]
    public void RealInvoice_ASF0021438_Header_Passes()
    {
        // 934,22 + 196,18 = 1 130,40
        Assert.Empty(Flags(934.22m, 196.18m, 1130.40m));
        var outcome = ExpenseService.EvaluateValidation(934.22m, 196.18m, 1130.40m, NoLines);
        Assert.Contains(En16931TotalsValidator.BrCo15, outcome.Totals.PassedRules);
    }

    [Fact]
    public void RealInvoice_213_ZeroNetHugeGross_MissingMoneyField_RulesNotApplicable()
    {
        // 0,00 / 465 374,45: net ≤ 0 → null, so BR-CO-15 cannot run; MISSING_MONEY_FIELD stops the invoice
        var flags = Flags(0m, 0m, 465374.45m);
        Assert.Equal(new[] { OcrFlag.MissingMoneyField }, flags);

        var outcome = ExpenseService.EvaluateValidation(0m, 0m, 465374.45m, NoLines);
        Assert.Contains(outcome.Totals.NotApplicable, n => n.RuleId == En16931TotalsValidator.BrCo15 && n.MissingInputs.Contains("BT-109"));
        Assert.DoesNotContain(En16931TotalsValidator.BrCo15, outcome.Totals.PassedRules);
    }

    [Fact]
    public void NegativeGross_IsNotExtracted_MissingMoneyField()
    {
        var flags = Flags(-100m, -21m, -121m);
        Assert.Equal(new[] { OcrFlag.MissingMoneyField }, flags);
    }

    [Fact]
    public void ZeroVat_IsPresentZero_NetMustEqualGross()
    {
        // VAT 0 is a real 0, not "missing": 100 + 0 ≠ 121 is a BR-CO-15 violation
        Assert.Contains(OcrFlag.AmountArithmeticMismatch, Flags(100m, 0m, 121m));
        var outcome = ExpenseService.EvaluateValidation(100m, 0m, 121m, NoLines);
        Assert.DoesNotContain(outcome.Totals.NotApplicable, n => n.RuleId == En16931TotalsValidator.BrCo15);
    }

    [Fact]
    public void ThreeDecimalNet_SchematronRounding_Passes()
    {
        // round((100.004 + 21) × 100) / 100 = 121.00
        Assert.Empty(Flags(100.004m, 21m, 121m));
    }

    [Fact]
    public void HeaderNetFeedsBt106AndBt109_BrCo13AlwaysPasses_BrCo16AlwaysNotApplicable()
    {
        var lines = new[] { new ExpenseService.ValidationLine(50m, null, null) };
        var outcome = ExpenseService.EvaluateValidation(100m, 21m, 121m, lines);

        Assert.Contains(En16931TotalsValidator.BrCo13, outcome.Totals.PassedRules);
        Assert.Contains(outcome.Totals.NotApplicable, n => n.RuleId == En16931TotalsValidator.BrCo16 && n.MissingInputs.SequenceEqual(new[] { "BT-115" }));
        // BT-106 is the header net, not the line sum: BR-CO-10 compares 100 with the lines (50)
        Assert.Contains(outcome.Totals.Violations, v => v.RuleId == En16931TotalsValidator.BrCo10 && v.Actual == 100m && v.Expected == 50m);
    }

    [Fact]
    public void NoLines_BrCo10NotApplicable()
    {
        var outcome = ExpenseService.EvaluateValidation(100m, 21m, 121m, NoLines);
        Assert.Contains(outcome.Totals.NotApplicable, n => n.RuleId == En16931TotalsValidator.BrCo10 && n.MissingInputs.Contains("BT-131"));
    }

    [Fact]
    public void AmountsBeyondDecimalRange_TotalsOutOfRange()
    {
        var flags = Flags(1e27m, 1m, 1e27m);

        Assert.Contains(OcrFlag.TotalsOutOfRange, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
    }

    // ---------- BR-CO-10 bands (D-040) ----------

    private static ExpenseService.ValidationLine[] Lines(params decimal[] nets) =>
        nets.Select(n => new ExpenseService.ValidationLine(n, 1m, null)).ToArray();

    [Fact]
    public void BrCo10_LinesEqualHeader_NoLineFlag()
    {
        var flags = Flags(100m, 21m, 121m, Lines(60m, 40m));

        Assert.DoesNotContain(OcrFlag.AmountMismatch, flags);
        Assert.DoesNotContain(OcrFlag.LineSumRounding, flags);
    }

    [Theory]
    [InlineData(99.99)]   // 0.01
    [InlineData(99.98)]   // 0.02
    [InlineData(99.95)]   // 0.05 — the band's upper edge is still information
    [InlineData(100.05)]  // lines above the header
    public void BrCo10_DifferenceUpTo5Cents_LineSumRounding_Information(double lineNet)
    {
        var flags = Flags(100m, 21m, 121m, Lines((decimal)lineNet));

        Assert.Contains(OcrFlag.LineSumRounding, flags);
        Assert.DoesNotContain(OcrFlag.AmountMismatch, flags);
    }

    [Theory]
    [InlineData(99.94)]   // 0.06
    [InlineData(100.06)]
    [InlineData(50)]
    public void BrCo10_DifferenceAbove5Cents_AmountMismatch(double lineNet)
    {
        var flags = Flags(100m, 21m, 121m, Lines((decimal)lineNet));

        Assert.Contains(OcrFlag.AmountMismatch, flags);
        Assert.DoesNotContain(OcrFlag.LineSumRounding, flags);
    }

    [Fact]
    public void BrCo10_LineSumRoundedPerSchematron_Passes()
    {
        // 33.333 × 3 = 99.999 → round(99.999 × 100) / 100 = 100.00 = header
        var flags = Flags(100m, 21m, 121m, Lines(33.333m, 33.333m, 33.333m));

        Assert.DoesNotContain(OcrFlag.AmountMismatch, flags);
        Assert.DoesNotContain(OcrFlag.LineSumRounding, flags);
    }

    [Fact]
    public void RealInvoice_370_LinesFromBePvmColumn_AmountMismatch()
    {
        // 370: lines sum 506,06 („Be PVM" column read as line nets) vs header net 418,24.
        // The two-line split is synthetic; the sum and header are the real figures.
        var flags = Flags(418.24m, 87.83m, 506.07m, Lines(300.00m, 206.06m));

        Assert.Contains(OcrFlag.AmountMismatch, flags);
        Assert.DoesNotContain(OcrFlag.AmountArithmeticMismatch, flags);
    }

    [Fact]
    public void BrCo10_HeaderNetMissing_NotApplicable_NoLineFlag()
    {
        // net ≤ 0 → null: BR-CO-10 cannot run; MISSING_MONEY_FIELD carries the stop
        var flags = Flags(0m, 0m, 465374.45m, Lines(465374.45m));

        Assert.Equal(new[] { OcrFlag.MissingMoneyField }, flags);
    }

    [Fact]
    public void StaleOwnedFlags_Dropped_OtherFlagsKept()
    {
        var flags = new List<string>
        {
            OcrFlag.OwnCompany, OcrFlag.AmountArithmeticMismatch, OcrFlag.MissingMoneyField, OcrFlag.TotalsOutOfRange,
            OcrFlag.AmountMismatch, OcrFlag.LineSumRounding
        };

        ExpenseService.RecomputeValidationFlags(flags, 100m, 21m, 121m, NoLines);

        Assert.Equal(new[] { OcrFlag.OwnCompany }, flags);
    }
}
