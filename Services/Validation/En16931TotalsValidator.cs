using System.Globalization;

namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Document-level amounts for the EN 16931 BR-CO arithmetic rules. Every value is nullable:
/// null means "not extracted", which is different from 0. Plain record, not OcrResultDto.
/// </summary>
public sealed record En16931TotalsInput
{
    /// <summary>BT-131 invoice line net amounts.</summary>
    public IReadOnlyList<decimal?>? LineNetAmounts { get; init; }

    /// <summary>BT-106 sum of invoice line net amounts.</summary>
    public decimal? SumOfLineNet { get; init; }

    /// <summary>BT-107 sum of document-level allowances (optional; absent = 0).</summary>
    public decimal? AllowanceTotal { get; init; }

    /// <summary>BT-108 sum of document-level charges (optional; absent = 0).</summary>
    public decimal? ChargeTotal { get; init; }

    /// <summary>BT-109 invoice total amount without VAT.</summary>
    public decimal? TotalWithoutVat { get; init; }

    /// <summary>BT-110 invoice total VAT amount.</summary>
    public decimal? VatTotal { get; init; }

    /// <summary>BT-112 invoice total amount with VAT.</summary>
    public decimal? TotalWithVat { get; init; }

    /// <summary>BT-113 paid amount (optional; absent = 0).</summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>BT-114 rounding amount (optional; absent = 0).</summary>
    public decimal? RoundingAmount { get; init; }

    /// <summary>BT-115 amount due for payment.</summary>
    public decimal? AmountDue { get; init; }
}

/// <summary>
/// A rule whose identity did not hold. <see cref="Expected"/> is the right-hand side computed
/// from the other fields, <see cref="Actual"/> the stated field; both already rounded to 2 decimals.
/// </summary>
public sealed record TotalsRuleViolation(string RuleId, decimal Expected, decimal Actual, string Message);

/// <summary>A rule that could not be evaluated because required inputs were missing.</summary>
public sealed record TotalsRuleNotApplicable(string RuleId, IReadOnlyList<string> MissingInputs);

/// <summary>
/// Outcome of <see cref="En16931TotalsValidator.Validate"/>. Every rule appears in exactly one of
/// <see cref="PassedRules"/>, <see cref="Violations"/> or <see cref="NotApplicable"/> — a rule
/// with missing inputs is never counted as passed.
/// </summary>
public sealed record En16931TotalsResult
{
    public required IReadOnlyList<string> PassedRules { get; init; }
    public required IReadOnlyList<TotalsRuleViolation> Violations { get; init; }
    public required IReadOnlyList<TotalsRuleNotApplicable> NotApplicable { get; init; }
}

/// <summary>
/// EN 16931 header arithmetic rules BR-CO-10, BR-CO-13, BR-CO-15, BR-CO-16. Pure static, no I/O —
/// OCR Etapas 1 prep, not yet wired in.
/// <para>
/// Comparison semantics follow the official Schematron (ConnectingEurope/eInvoicing-EN16931,
/// ubl/schematron/preprocessed/EN16931-UBL-validation-preprocessed.sch, which rounds with
/// <c>round(x * 10 * 10) div 100</c>): both sides are rounded to 2 decimals with XPath
/// <c>round()</c> semantics — <c>floor(x × 100 + 0.5) / 100</c>, halves toward +∞ also for
/// negatives — and then compared for exact equality. No flat tolerance.
/// </para>
/// <para>
/// Missing inputs: BT-107, BT-108, BT-113 and BT-114 are optional in EN 16931 and default to 0 when
/// absent. Any other missing input makes the rule NOT APPLICABLE (listed in
/// <see cref="En16931TotalsResult.NotApplicable"/>), never silently passed.
/// </para>
/// </summary>
public static class En16931TotalsValidator
{
    public const string BrCo10 = "BR-CO-10";
    public const string BrCo13 = "BR-CO-13";
    public const string BrCo15 = "BR-CO-15";
    public const string BrCo16 = "BR-CO-16";

    // Lithuanian-style amounts for user-facing messages: "1 130,40". Fixed format so output does
    // not depend on the host's ICU/culture data.
    private static readonly NumberFormatInfo LtAmountFormat = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
        NumberDecimalDigits = 2,
        NegativeSign = "-"
    };

    public static En16931TotalsResult Validate(En16931TotalsInput input)
    {
        var passed = new List<string>();
        var violations = new List<TotalsRuleViolation>();
        var notApplicable = new List<TotalsRuleNotApplicable>();

        void Record(string ruleId, List<string> missing, Func<TotalsRuleViolation?> evaluate)
        {
            if (missing.Count > 0)
            {
                notApplicable.Add(new TotalsRuleNotApplicable(ruleId, missing));
                return;
            }
            var violation = evaluate();
            if (violation is null) passed.Add(ruleId);
            else violations.Add(violation);
        }

        // BR-CO-10: BT-106 = Σ BT-131
        var missing10 = new List<string>();
        if (input.SumOfLineNet is null) missing10.Add("BT-106");
        if (input.LineNetAmounts is null || input.LineNetAmounts.Count == 0 || input.LineNetAmounts.Any(a => a is null))
            missing10.Add("BT-131");
        Record(BrCo10, missing10, () =>
        {
            var expected = XPathRound2(input.LineNetAmounts!.Sum(a => a!.Value));
            var actual = XPathRound2(input.SumOfLineNet!.Value);
            return expected == actual ? null : new TotalsRuleViolation(BrCo10, expected, actual,
                $"{BrCo10} pažeista: eilučių suma (BT-106) {Lt(actual)} ≠ eilučių sumų be PVM suma {Lt(expected)}");
        });

        // BR-CO-13: BT-109 = BT-106 − BT-107 + BT-108
        var missing13 = new List<string>();
        if (input.TotalWithoutVat is null) missing13.Add("BT-109");
        if (input.SumOfLineNet is null) missing13.Add("BT-106");
        Record(BrCo13, missing13, () =>
        {
            var allowances = input.AllowanceTotal ?? 0m;
            var charges = input.ChargeTotal ?? 0m;
            var expected = XPathRound2(input.SumOfLineNet!.Value - allowances + charges);
            var actual = XPathRound2(input.TotalWithoutVat!.Value);
            return expected == actual ? null : new TotalsRuleViolation(BrCo13, expected, actual,
                $"{BrCo13} pažeista: suma be PVM {Lt(actual)} ≠ eilučių suma {Lt(input.SumOfLineNet!.Value)} − nuolaidos {Lt(allowances)} + priemokos {Lt(charges)}");
        });

        // BR-CO-15: BT-112 = BT-109 + BT-110
        var missing15 = new List<string>();
        if (input.TotalWithVat is null) missing15.Add("BT-112");
        if (input.TotalWithoutVat is null) missing15.Add("BT-109");
        if (input.VatTotal is null) missing15.Add("BT-110");
        Record(BrCo15, missing15, () =>
        {
            var expected = XPathRound2(input.TotalWithoutVat!.Value + input.VatTotal!.Value);
            var actual = XPathRound2(input.TotalWithVat!.Value);
            return expected == actual ? null : new TotalsRuleViolation(BrCo15, expected, actual,
                $"{BrCo15} pažeista: suma su PVM {Lt(actual)} ≠ suma be PVM {Lt(input.TotalWithoutVat!.Value)} + PVM {Lt(input.VatTotal!.Value)}");
        });

        // BR-CO-16: BT-115 = BT-112 − BT-113 + BT-114
        var missing16 = new List<string>();
        if (input.AmountDue is null) missing16.Add("BT-115");
        if (input.TotalWithVat is null) missing16.Add("BT-112");
        Record(BrCo16, missing16, () =>
        {
            var paid = input.PaidAmount ?? 0m;
            var rounding = input.RoundingAmount ?? 0m;
            var expected = XPathRound2(input.TotalWithVat!.Value - paid + rounding);
            var actual = XPathRound2(input.AmountDue!.Value);
            return expected == actual ? null : new TotalsRuleViolation(BrCo16, expected, actual,
                $"{BrCo16} pažeista: mokėtina suma {Lt(actual)} ≠ suma su PVM {Lt(input.TotalWithVat!.Value)} − sumokėta {Lt(paid)} + apvalinimas {Lt(rounding)}");
        });

        return new En16931TotalsResult { PassedRules = passed, Violations = violations, NotApplicable = notApplicable };
    }

    /// <summary>
    /// XPath <c>round(x * 100) div 100</c>: <c>floor(x × 100 + 0.5) / 100</c>. Halves round toward
    /// +∞ for both signs (2.345 → 2.35, −2.345 → −2.34), unlike <see cref="Math.Round(decimal, int)"/>.
    /// </summary>
    public static decimal XPathRound2(decimal value) => Math.Floor(value * 100m + 0.5m) / 100m;

    // Shared with LineAmountPlausibilityRule so all validation messages format amounts the same way.
    internal static string Lt(decimal amount) => amount.ToString("N2", LtAmountFormat);
}
