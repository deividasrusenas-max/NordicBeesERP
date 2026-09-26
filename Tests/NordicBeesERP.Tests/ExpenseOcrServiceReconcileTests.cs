using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7(c) — D-038 Q7 / D-041: the reconcile step no longer deletes lines with quantity &gt; 1000 or with a
/// repeated description. Where it used to, the lines are kept and the invoice carries the information flags
/// LINE_LARGE_QUANTITY / LINE_DUPLICATE_DESCRIPTION. Zero-amount lines are still dropped. Pure tests of
/// <see cref="ExpenseOcrService.ReconcileLines"/>.
/// </summary>
public class ExpenseOcrServiceReconcileTests
{
    private static OcrLineDto Line(string description, decimal amount, decimal? quantity = 1m) =>
        new() { Description = description, AmountExclVat = amount, Quantity = quantity };

    private static OcrResultDto Result(decimal headerExcl, params OcrLineDto[] lines)
    {
        var result = new OcrResultDto { AmountExclVat = headerExcl };
        result.Lines.AddRange(lines);
        return result;
    }

    private static void Reconcile(OcrResultDto result) => ExpenseOcrService.ReconcileLines(result, NullLogger.Instance);

    // ---------- the real ASF0021438 9-unit line ----------

    [Fact]
    public void Asf0021438_TheRealNineUnitLine_IsNoLongerDeleted_ItIsKeptAndFlagged()
    {
        // header net 934,22; the gross-column line amounts sum to 1 130,40; Azure read the 9-unit line's quantity as 9000
        var result = OcrFixtures.Dto(OcrFixtures.Asf0021438());
        // (the fixture DTO carries numbers only; ProcessAsync also sets the descriptions)
        result.Lines[0].Description = "Kuras A";
        result.Lines[1].Description = "Kuras B";
        Assert.Equal(2, result.Lines.Count);

        Reconcile(result);

        Assert.Equal(2, result.Lines.Count);
        var kept = Assert.Single(result.Lines, l => l.Quantity == 9000m);
        Assert.Equal(158.40m, kept.AmountExclVat);
        Assert.Contains(OcrFlag.LineLargeQuantity, result.Flags);
        Assert.DoesNotContain(OcrFlag.LineDuplicateDescription, result.Flags);
    }

    [Fact]
    public void CorrectLargeQuantity_FuelLitres_IsKept_LikeTheCorpusLines()
    {
        // 1600 l at 0,115 — a correct quantity (the corpus has three such lines); the lines exceed the header
        var result = Result(100m, Line("Kuras", 184m, 1600m), Line("Kita", 5m, 1m));

        Reconcile(result);

        Assert.Equal(2, result.Lines.Count);
        Assert.Contains(OcrFlag.LineLargeQuantity, result.Flags);
    }

    // ---------- duplicate descriptions ----------

    [Fact]
    public void DuplicateDescriptions_AreKept_AndFlagged()
    {
        var result = Result(100m, Line("Transportas", 60m), Line("Transportas", 60m), Line("Muitas", 10m));

        Reconcile(result);

        Assert.Equal(3, result.Lines.Count);
        Assert.Contains(OcrFlag.LineDuplicateDescription, result.Flags);
        Assert.DoesNotContain(OcrFlag.LineLargeQuantity, result.Flags);
    }

    [Fact]
    public void BothHeuristicsAtOnce_BothFlags_NothingDeleted()
    {
        var result = Result(50m, Line("A", 60m, 5000m), Line("A", 60m, 1m));

        Reconcile(result);

        Assert.Equal(2, result.Lines.Count);
        Assert.Contains(OcrFlag.LineLargeQuantity, result.Flags);
        Assert.Contains(OcrFlag.LineDuplicateDescription, result.Flags);
    }

    // ---------- when nothing happens ----------

    [Fact]
    public void LinesMatchTheHeader_NoFlags_EvenWithLargeQuantityAndDuplicates()
    {
        var result = Result(100m, Line("A", 50m, 5000m), Line("A", 50m, 1m));

        Reconcile(result);

        Assert.Equal(2, result.Lines.Count);
        Assert.Empty(result.Flags);
    }

    [Fact]
    public void LinesExceedTheHeaderByExactlyFiveCents_IsNotEnough()
    {
        var result = Result(100m, Line("A", 60m, 5000m), Line("A", 40.05m, 1m));

        Reconcile(result);

        Assert.Empty(result.Flags);
    }

    [Fact]
    public void LinesExceedTheHeaderBySixCents_ActsAsBefore()
    {
        var result = Result(100m, Line("A", 60m, 5000m), Line("B", 40.06m, 1m));

        Reconcile(result);

        Assert.Contains(OcrFlag.LineLargeQuantity, result.Flags);
    }

    [Fact]
    public void LinesBelowTheHeader_NothingHappens()
    {
        var result = Result(100m, Line("A", 10m, 5000m), Line("A", 10m, 1m));

        Reconcile(result);

        Assert.Equal(2, result.Lines.Count);
        Assert.Empty(result.Flags);
    }

    [Fact]
    public void NoHeaderTotal_OrNoLines_NothingHappens()
    {
        var noHeader = Result(0m, Line("A", 60m, 5000m), Line("A", 0m, 1m));
        var noLines = Result(100m);

        Reconcile(noHeader);
        Reconcile(noLines);

        Assert.Equal(2, noHeader.Lines.Count); // even the zero-amount line: no header, no reconcile
        Assert.Empty(noHeader.Flags);
        Assert.Empty(noLines.Flags);
    }

    // ---------- zero-amount lines: unchanged ----------

    [Fact]
    public void ZeroAmountLines_AreStillRemoved_WhenLinesExceedTheHeader()
    {
        var result = Result(100m, Line("Prekė", 150m), Line("Nulinė", 0m));

        Reconcile(result);

        Assert.Equal("Prekė", Assert.Single(result.Lines).Description);
    }

    [Fact]
    public void ZeroAmountLinesAreRemovedFirst_AndOnlyTheRemainingLinesAreJudged()
    {
        // the remaining line still exceeds the header (by 0,20), but the removed zero-amount lines' 5000 quantities
        // and repeated description are gone with them — nothing to report
        var result = Result(100m, Line("Prekė", 100.20m), Line("Nulinė", 0m, 5000m), Line("Nulinė", 0m, 5000m));

        Reconcile(result);

        Assert.Equal("Prekė", Assert.Single(result.Lines).Description);
        Assert.Empty(result.Flags);
    }

    [Fact]
    public void ExistingFlags_AreKept_AndNotDuplicated()
    {
        var result = Result(50m, Line("A", 60m, 5000m), Line("B", 60m, 1m));
        result.Flags.Add(OcrFlag.VendorNotFound);
        result.Flags.Add(OcrFlag.LineLargeQuantity);

        Reconcile(result);

        Assert.Equal(new[] { OcrFlag.VendorNotFound, OcrFlag.LineLargeQuantity }, result.Flags);
    }

    [Fact]
    public void Reconcile_NeverChangesAValue()
    {
        var result = Result(100m, Line("A", 150m, 5000m), Line("A", 75.5m, 3m));

        Reconcile(result);

        Assert.Equal(new[] { 150m, 75.5m }, result.Lines.Select(l => l.AmountExclVat).ToArray());
        Assert.Equal(new decimal?[] { 5000m, 3m }, result.Lines.Select(l => l.Quantity).ToArray());
        Assert.Equal(100m, result.AmountExclVat);
    }
}
