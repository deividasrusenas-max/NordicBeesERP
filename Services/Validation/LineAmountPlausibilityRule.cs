namespace NordicBeesERP.Services.Validation;

/// <summary>One invoice line for <see cref="LineAmountPlausibilityRule"/>; null = not extracted.</summary>
public sealed record LineAmountInput(int LineNumber, decimal? Quantity, decimal? UnitPrice, decimal? LineNet);

/// <summary>A line whose quantity × unit price is outside tolerance of its line net amount.</summary>
public sealed record LineAmountViolation(int LineNumber, decimal Computed, decimal LineNet, decimal Tolerance, string Message);

/// <summary>
/// A line whose arithmetic overflows <see cref="decimal"/> (quantity × unit price, or the difference
/// to the line net, beyond ±7.9e28). Input out of range — never an exception.
/// </summary>
public sealed record LineAmountOutOfRange(int LineNumber, string Message);

/// <summary>Outcome of <see cref="LineAmountPlausibilityRule.Check"/>; every line lands in exactly one list.</summary>
public sealed record LineAmountResult
{
    public required IReadOnlyList<int> PassedLines { get; init; }
    public required IReadOnlyList<LineAmountViolation> Violations { get; init; }

    /// <summary>Lines with quantity, unit price or line net missing — not evaluated, never passed.</summary>
    public required IReadOnlyList<int> NotApplicableLines { get; init; }

    /// <summary>Lines whose arithmetic overflows <see cref="decimal"/> — not evaluated, never passed.</summary>
    public required IReadOnlyList<LineAmountOutOfRange> OutOfRangeLines { get; init; }
}

/// <summary>
/// PROJECT RULE — NOT EN 16931. Line-level plausibility check: quantity × unit price ≈ line net
/// amount, within <c>max(0.01, 0.5 % of |line net|)</c>. Pure static, no I/O — OCR Etapas 1 prep,
/// not yet wired in.
/// <para>
/// Projekto sprendimas, ne EN 16931 reikalavimas (BT-131 deklaruojama siuntėjo).
/// </para>
/// <para>
/// Never throws for any decimal input: a line whose arithmetic overflows <see cref="decimal"/> goes to
/// <see cref="LineAmountResult.OutOfRangeLines"/>, the same pattern as
/// <see cref="En16931TotalsValidator"/>.
/// </para>
/// </summary>
public static class LineAmountPlausibilityRule
{
    public const string RuleId = "PROJ-LINE-QTY-PRICE";

    private const decimal MinTolerance = 0.01m;
    private const decimal RelativeTolerance = 0.005m;

    public static LineAmountResult Check(IEnumerable<LineAmountInput> lines)
    {
        var passed = new List<int>();
        var violations = new List<LineAmountViolation>();
        var notApplicable = new List<int>();
        var outOfRange = new List<LineAmountOutOfRange>();

        foreach (var line in lines)
        {
            if (line.Quantity is null || line.UnitPrice is null || line.LineNet is null)
            {
                notApplicable.Add(line.LineNumber);
                continue;
            }

            decimal computed;
            decimal tolerance;
            bool withinTolerance;
            try
            {
                computed = line.Quantity.Value * line.UnitPrice.Value;
                // Math.Abs cannot overflow for decimal (its range is symmetric); 0.005 × |net| cannot either.
                tolerance = Math.Max(MinTolerance, RelativeTolerance * Math.Abs(line.LineNet.Value));
                withinTolerance = Math.Abs(computed - line.LineNet.Value) <= tolerance;
            }
            catch (OverflowException)
            {
                outOfRange.Add(new LineAmountOutOfRange(line.LineNumber,
                    $"Projekto taisyklė {RuleId} nepatikrinta {line.LineNumber} eilutėje: sumos per didelės, skaičiavimas viršija leistiną intervalą"));
                continue;
            }

            if (withinTolerance)
            {
                passed.Add(line.LineNumber);
                continue;
            }

            violations.Add(new LineAmountViolation(line.LineNumber, computed, line.LineNet.Value, tolerance,
                $"Projekto taisyklė {RuleId} (ne EN 16931) pažeista {line.LineNumber} eilutėje: kiekis × kaina {En16931TotalsValidator.Lt(computed)} ≠ eilutės suma be PVM {En16931TotalsValidator.Lt(line.LineNet.Value)}"));
        }

        return new LineAmountResult
        {
            PassedLines = passed, Violations = violations, NotApplicableLines = notApplicable, OutOfRangeLines = outOfRange
        };
    }
}
