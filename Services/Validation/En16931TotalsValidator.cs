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

    /// <summary>BT-107 sum of document-level allowances (optional; presence selects the BR-CO-13 case).</summary>
    public decimal? AllowanceTotal { get; init; }

    /// <summary>BT-108 sum of document-level charges (optional; presence selects the BR-CO-13 case).</summary>
    public decimal? ChargeTotal { get; init; }

    /// <summary>BT-109 invoice total amount without VAT.</summary>
    public decimal? TotalWithoutVat { get; init; }

    /// <summary>BT-110 invoice total VAT amount.</summary>
    public decimal? VatTotal { get; init; }

    /// <summary>BT-112 invoice total amount with VAT.</summary>
    public decimal? TotalWithVat { get; init; }

    /// <summary>BT-113 paid amount (optional; presence selects the BR-CO-16 case).</summary>
    public decimal? PaidAmount { get; init; }

    /// <summary>BT-114 rounding amount (optional; presence selects the BR-CO-16 case).</summary>
    public decimal? RoundingAmount { get; init; }

    /// <summary>BT-115 amount due for payment.</summary>
    public decimal? AmountDue { get; init; }
}

/// <summary>
/// A rule whose Schematron test did not hold. <see cref="Actual"/> and <see cref="Expected"/> are
/// the left- and right-hand operands of the Schematron comparison, evaluated exactly as the
/// Schematron writes them — rounded only where the Schematron applies <c>round()</c>. So a stated
/// amount with more than 2 decimals is usually NOT rounded here.
/// </summary>
public sealed record TotalsRuleViolation(string RuleId, decimal Expected, decimal Actual, string Message);

/// <summary>A rule that could not be evaluated because required inputs were missing.</summary>
public sealed record TotalsRuleNotApplicable(string RuleId, IReadOnlyList<string> MissingInputs);

/// <summary>
/// A rule that could not be evaluated because its arithmetic overflows <see cref="decimal"/>
/// (roughly |amount| ≥ 7.9e26 once multiplied by 100). Input out of range — never an exception.
/// </summary>
public sealed record TotalsRuleOutOfRange(string RuleId, string Message);

/// <summary>
/// Outcome of <see cref="En16931TotalsValidator.Validate"/>. Every rule appears in exactly one of
/// <see cref="PassedRules"/>, <see cref="Violations"/>, <see cref="NotApplicable"/> or
/// <see cref="OutOfRange"/> — a rule that was not evaluated is never counted as passed.
/// </summary>
public sealed record En16931TotalsResult
{
    public required IReadOnlyList<string> PassedRules { get; init; }
    public required IReadOnlyList<TotalsRuleViolation> Violations { get; init; }
    public required IReadOnlyList<TotalsRuleNotApplicable> NotApplicable { get; init; }
    public required IReadOnlyList<TotalsRuleOutOfRange> OutOfRange { get; init; }
}

/// <summary>
/// EN 16931 header arithmetic rules BR-CO-10, BR-CO-13, BR-CO-15, BR-CO-16. Pure static, no I/O —
/// OCR Etapas 1 prep, not yet wired in.
/// <para>
/// Each rule is a literal port of its <c>test</c> expression in the official Schematron
/// (ConnectingEurope/eInvoicing-EN16931, <c>ubl/schematron/preprocessed/EN16931-UBL-validation-preprocessed.sch</c>,
/// master as fetched 2026-09-26; the four expressions are identical in <c>ubl/schematron/UBL/EN16931-UBL-model.sch</c>).
/// Rounding follows that text literally: an operand is rounded only where the Schematron wraps it
/// in <c>round(x * 10 * 10) div 100</c>, using XPath <c>round()</c> semantics —
/// <c>floor(x × 100 + 0.5) / 100</c>, halves toward +∞ also for negatives. Stated amounts are
/// mostly compared unrounded; BR-CO-13 without allowances and charges, and BR-CO-16 without paid and
/// rounding amounts, compare with no rounding at all; BR-CO-16 with a rounding amount rounds
/// <c>BT-115 − BT-114</c>. Comparison is exact equality, no flat tolerance.
/// </para>
/// <para>
/// Missing inputs: BT-107, BT-108, BT-113 and BT-114 are optional. Their absence selects a case of
/// the Schematron test (they are not simply treated as 0 — e.g. BR-CO-13 with none of them compares
/// BT-109 to BT-106 unrounded). Any other missing input means "not extracted" and makes the rule NOT
/// APPLICABLE (listed in <see cref="En16931TotalsResult.NotApplicable"/>), never silently passed.
/// </para>
/// <para>
/// Never throws for any decimal input: arithmetic that overflows <see cref="decimal"/> (the
/// Schematron's xs:decimal has no such limit) puts the rule in
/// <see cref="En16931TotalsResult.OutOfRange"/>. Known limit: sums needing more than ~28 significant
/// digits are rounded by <see cref="decimal"/> arithmetic, where xs:decimal would stay exact —
/// irrelevant for real invoice amounts.
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
        var outOfRange = new List<TotalsRuleOutOfRange>();

        void Record(string ruleId, List<string> missing, Func<TotalsRuleViolation?> evaluate)
        {
            if (missing.Count > 0)
            {
                notApplicable.Add(new TotalsRuleNotApplicable(ruleId, missing));
                return;
            }
            TotalsRuleViolation? violation;
            try
            {
                violation = evaluate();
            }
            catch (OverflowException)
            {
                outOfRange.Add(new TotalsRuleOutOfRange(ruleId,
                    $"{ruleId} nepatikrinta: sumos per didelės, skaičiavimas viršija leistiną intervalą"));
                return;
            }
            if (violation is null) passed.Add(ruleId);
            else violations.Add(violation);
        }

        // BR-CO-10: xs:decimal(cbc:LineExtensionAmount) = xs:decimal(round(sum(//(cac:InvoiceLine|cac:CreditNoteLine)/xs:decimal(cbc:LineExtensionAmount)) * 10 * 10) div 100)
        var missing10 = new List<string>();
        if (input.SumOfLineNet is null) missing10.Add("BT-106");
        if (input.LineNetAmounts is null || input.LineNetAmounts.Count == 0 || input.LineNetAmounts.Any(a => a is null))
            missing10.Add("BT-131");
        Record(BrCo10, missing10, () =>
        {
            var actual = input.SumOfLineNet!.Value;
            var expected = XPathRound2(input.LineNetAmounts!.Sum(a => a!.Value));
            return actual == expected ? null : new TotalsRuleViolation(BrCo10, expected, actual,
                $"{BrCo10} pažeista: eilučių suma (BT-106) {LtExact(actual)} ≠ eilučių sumų be PVM suma {LtExact(expected)}");
        });

        // BR-CO-13, four cases by presence of ChargeTotalAmount (BT-108) / AllowanceTotalAmount (BT-107):
        //   both:           BT-109 = round((BT-106 + BT-108 - BT-107) * 10 * 10) div 100
        //   allowance only: BT-109 = round((BT-106 - BT-107) * 10 * 10) div 100
        //   charge only:    BT-109 = round((BT-106 + BT-108) * 10 * 10) div 100
        //   neither:        BT-109 = BT-106   (no rounding)
        var missing13 = new List<string>();
        if (input.TotalWithoutVat is null) missing13.Add("BT-109");
        if (input.SumOfLineNet is null) missing13.Add("BT-106");
        Record(BrCo13, missing13, () =>
        {
            var lineNet = input.SumOfLineNet!.Value;
            var actual = input.TotalWithoutVat!.Value;
            var expected = (input.ChargeTotal, input.AllowanceTotal) switch
            {
                ({ } c, { } a) => XPathRound2(lineNet + c - a),
                (null, { } a) => XPathRound2(lineNet - a),
                ({ } c, null) => XPathRound2(lineNet + c),
                (null, null) => lineNet
            };
            return actual == expected ? null : new TotalsRuleViolation(BrCo13, expected, actual,
                $"{BrCo13} pažeista: suma be PVM {LtExact(actual)} ≠ eilučių suma {LtExact(lineNet)} − nuolaidos {LtExact(input.AllowanceTotal ?? 0m)} + priemokos {LtExact(input.ChargeTotal ?? 0m)}");
        });

        // BR-CO-15: BT-112 = round((BT-109 + BT-110) * 10 * 10) div 100
        // (the Schematron's count(TaxAmount in document currency) eq 1 is implied by one BT-110 input)
        var missing15 = new List<string>();
        if (input.TotalWithVat is null) missing15.Add("BT-112");
        if (input.TotalWithoutVat is null) missing15.Add("BT-109");
        if (input.VatTotal is null) missing15.Add("BT-110");
        Record(BrCo15, missing15, () =>
        {
            var actual = input.TotalWithVat!.Value;
            var expected = XPathRound2(input.TotalWithoutVat!.Value + input.VatTotal!.Value);
            return actual == expected ? null : new TotalsRuleViolation(BrCo15, expected, actual,
                $"{BrCo15} pažeista: suma su PVM {LtExact(actual)} ≠ suma be PVM {LtExact(input.TotalWithoutVat!.Value)} + PVM {LtExact(input.VatTotal!.Value)}");
        });

        // BR-CO-16, four cases by presence of PrepaidAmount (BT-113) / PayableRoundingAmount (BT-114):
        //   paid only:     BT-115 = round((BT-112 - BT-113) * 10 * 10) div 100
        //   neither:       BT-115 = BT-112   (no rounding)
        //   both:          round((BT-115 - BT-114) * 10 * 10) div 100 = round((BT-112 - BT-113) * 10 * 10) div 100
        //   rounding only: round((BT-115 - BT-114) * 10 * 10) div 100 = BT-112
        var missing16 = new List<string>();
        if (input.AmountDue is null) missing16.Add("BT-115");
        if (input.TotalWithVat is null) missing16.Add("BT-112");
        Record(BrCo16, missing16, () =>
        {
            var due = input.AmountDue!.Value;
            var withVat = input.TotalWithVat!.Value;
            var (actual, expected) = (input.PaidAmount, input.RoundingAmount) switch
            {
                ({ } p, null) => (due, XPathRound2(withVat - p)),
                (null, null) => (due, withVat),
                ({ } p, { } r) => (XPathRound2(due - r), XPathRound2(withVat - p)),
                (null, { } r) => (XPathRound2(due - r), withVat)
            };
            return actual == expected ? null : new TotalsRuleViolation(BrCo16, expected, actual,
                $"{BrCo16} pažeista: mokėtina suma {LtExact(due)} ≠ suma su PVM {LtExact(withVat)} − sumokėta {LtExact(input.PaidAmount ?? 0m)} + apvalinimas {LtExact(input.RoundingAmount ?? 0m)}");
        });

        return new En16931TotalsResult
        {
            PassedRules = passed, Violations = violations, NotApplicable = notApplicable, OutOfRange = outOfRange
        };
    }

    /// <summary>
    /// XPath <c>round(x * 10 * 10) div 100</c>: <c>floor(x × 100 + 0.5) / 100</c>. Halves round toward
    /// +∞ for both signs (2.345 → 2.35, −2.345 → −2.34), unlike <see cref="Math.Round(decimal, int)"/>.
    /// Throws <see cref="OverflowException"/> for |x| ≳ 7.9e26; <see cref="Validate"/> catches it.
    /// </summary>
    public static decimal XPathRound2(decimal value) => Math.Floor(value * 100m + 0.5m) / 100m;

    // Shared with LineAmountPlausibilityRule so all validation messages format amounts the same way.
    internal static string Lt(decimal amount) => amount.ToString("N2", LtAmountFormat);

    // Like Lt, but keeps every decimal of an unrounded stated amount, so "15,004 ≠ 15,00" is not
    // shown as "15,00 ≠ 15,00".
    private static string LtExact(decimal amount) => amount.ToString("N" + Math.Max(2, (int)amount.Scale), LtAmountFormat);
}
