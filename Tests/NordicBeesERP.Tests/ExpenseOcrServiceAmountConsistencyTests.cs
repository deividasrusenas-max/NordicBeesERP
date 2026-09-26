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

    [Fact]
    public void StaleOwnedFlags_Dropped_OtherFlagsKept()
    {
        var flags = new List<string>
        {
            OcrFlag.OwnCompany, OcrFlag.AmountArithmeticMismatch, OcrFlag.MissingMoneyField, OcrFlag.TotalsOutOfRange
        };

        ExpenseService.RecomputeValidationFlags(flags, 100m, 21m, 121m, NoLines);

        Assert.Equal(new[] { OcrFlag.OwnCompany }, flags);
    }
}
