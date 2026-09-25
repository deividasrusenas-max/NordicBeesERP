namespace NordicBeesERP.Services.Validation;

/// <summary>One invoice line for <see cref="LineAmountPlausibilityRule"/>; null = not extracted.</summary>
public sealed record LineAmountInput(int LineNumber, decimal? Quantity, decimal? UnitPrice, decimal? LineNet);

/// <summary>A line whose quantity × unit price is outside tolerance of its line net amount.</summary>
public sealed record LineAmountViolation(int LineNumber, decimal Computed, decimal LineNet, decimal Tolerance, string Message);

/// <summary>Outcome of <see cref="LineAmountPlausibilityRule.Check"/>; every line lands in exactly one list.</summary>
public sealed record LineAmountResult
{
    public required IReadOnlyList<int> PassedLines { get; init; }
    public required IReadOnlyList<LineAmountViolation> Violations { get; init; }

    /// <summary>Lines with quantity, unit price or line net missing — not evaluated, never passed.</summary>
    public required IReadOnlyList<int> NotApplicableLines { get; init; }
}

/// <summary>
/// PROJECT RULE — NOT EN 16931. Line-level plausibility check: quantity × unit price ≈ line net
/// amount, within <c>max(0.01, 0.5 % of |line net|)</c>. Pure static, no I/O — OCR Etapas 1 prep,
/// not yet wired in.
/// <para>
/// Projekto sprendimas, ne EN 16931 reikalavimas (BT-131 deklaruojama siuntėjo).
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

        foreach (var line in lines)
        {
            if (line.Quantity is null || line.UnitPrice is null || line.LineNet is null)
            {
                notApplicable.Add(line.LineNumber);
                continue;
            }

            var computed = line.Quantity.Value * line.UnitPrice.Value;
            var tolerance = Math.Max(MinTolerance, RelativeTolerance * Math.Abs(line.LineNet.Value));

            if (Math.Abs(computed - line.LineNet.Value) <= tolerance)
            {
                passed.Add(line.LineNumber);
                continue;
            }

            violations.Add(new LineAmountViolation(line.LineNumber, computed, line.LineNet.Value, tolerance,
                $"Projekto taisyklė {RuleId} (ne EN 16931) pažeista {line.LineNumber} eilutėje: kiekis × kaina {En16931TotalsValidator.Lt(computed)} ≠ eilutės suma be PVM {En16931TotalsValidator.Lt(line.LineNet.Value)}"));
        }

        return new LineAmountResult { PassedLines = passed, Violations = violations, NotApplicableLines = notApplicable };
    }
}
