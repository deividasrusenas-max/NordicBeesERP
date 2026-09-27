using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// Etapas 3 S3 (PLAN-ETAPAS3.md §1 option (c), §8.1 S3, D-023): deterministic <c>tables[]</c> line-net repair. Pure,
/// synthetic-fixture tests only — no corpus dependency (the corpus-gated cases live in
/// <see cref="TableLineRepairCorpusTests"/> below, reusing <see cref="CorpusFactAttribute"/>).
/// </summary>
public class TableLineRepairTests
{
    // ---------- ASF0021438 shape: Items took the gross column for line 1, table has the correct net ----------

    [Fact]
    public void Asf0021438Shape_TableReconciles_ItemsDoNotRepairsLine1Net()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("961,71", 961.71), OcrFixtures.Cur("0", 0), OcrFixtures.Cur("961,71", 961.71),
            new[]
            {
                OcrFixtures.Line("Kuras A", OcrFixtures.Num("3 888,000", 3), OcrFixtures.Cur("0,2066", 0.2066), OcrFixtures.Cur("972,00", 972.00)), // wrong: gross, not net
                OcrFixtures.Line("Kuras B", OcrFixtures.Num("9,000", 9000), OcrFixtures.Cur("14,5456", 14.5456), OcrFixtures.Cur("158,40", 158.40))  // already correct
            },
            new[]
            {
                OcrTableFixtures.Table(3, 4,
                    OcrTableFixtures.HeaderCell(0, 0, "Aprašymas"), OcrTableFixtures.HeaderCell(0, 1, "Kiekis"),
                    OcrTableFixtures.HeaderCell(0, 2, "Kaina"), OcrTableFixtures.HeaderCell(0, 3, "Suma be PVM"),
                    OcrTableFixtures.Cell(1, 0, "Kuras A"), OcrTableFixtures.Cell(1, 1, "3 888,000"), OcrTableFixtures.Cell(1, 2, "0,2066"), OcrTableFixtures.Cell(1, 3, "803,31"),
                    OcrTableFixtures.Cell(2, 0, "Kuras B"), OcrTableFixtures.Cell(2, 1, "9,000"), OcrTableFixtures.Cell(2, 2, "14,5456"), OcrTableFixtures.Cell(2, 3, "158,40"))
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.Repaired, result.Outcome);
        Assert.Equal(new[] { 0 }, result.RepairedLineIndexes);
        Assert.Equal(803.31m, dto.Lines[0].AmountExclVat);
        Assert.NotEqual(972.00m, dto.Lines[0].AmountExclVat);
        Assert.Equal(158.40m, dto.Lines[1].AmountExclVat); // untouched — already correct
    }

    // ---------- EGO/UTA PL/370 shape: single line, Items took gross; table has an explicit net column ----------
    // Modelled on the real UTA PL corpus document's own structure (Etapas 3 S1 report): "Kwota netto / Suma netto"
    // net column, "Kwota brutto / Suma brutto" gross column, and a trailing "Łączna wartość" grand-total row that
    // repeats the same figures — this test also proves that row is excluded, not counted as a second line.

    [Fact]
    public void UtaPlShape_GrossTakenAsNet_TableHasExplicitNetColumn_RepairsToNet()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("268,30", 268.30), OcrFixtures.Cur("61,70", 61.70), OcrFixtures.Cur("330,00", 330.00),
            new[] { OcrFixtures.Line("Olej napędowy", OcrFixtures.Num("1", 1), OcrFixtures.Cur("330,00", 330.00), OcrFixtures.Cur("330,00", 330.00)) }, // wrong: gross taken as net
            new[]
            {
                OcrTableFixtures.Table(3, 4,
                    OcrTableFixtures.HeaderCell(0, 0, "Ilość / Kiekis"), OcrTableFixtures.HeaderCell(0, 1, "Kwota netto / Suma netto PLN"),
                    OcrTableFixtures.HeaderCell(0, 2, "VAT"), OcrTableFixtures.HeaderCell(0, 3, "Kwota brutto / Suma brutto PLN"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "268,30"), OcrTableFixtures.Cell(1, 2, "23,00"), OcrTableFixtures.Cell(1, 3, "330,00"),
                    OcrTableFixtures.Cell(2, 0, ""), OcrTableFixtures.Cell(2, 1, "268,30"), OcrTableFixtures.Cell(2, 2, "Łączna wartość"), OcrTableFixtures.Cell(2, 3, "330,00"))
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.Repaired, result.Outcome);
        Assert.Equal(268.30m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- Clean document: Items already reconciles — never touched ----------

    [Fact]
    public void CleanDocument_ItemsAlreadyReconciles_TableIgnoredEvenIfDifferent()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("100,00", 100.00), OcrFixtures.Cur("21,00", 21.00), OcrFixtures.Cur("121,00", 121.00),
            new[] { OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("100,00", 100.00), OcrFixtures.Cur("100,00", 100.00)) },
            new[]
            {
                OcrTableFixtures.Table(2, 2,
                    OcrTableFixtures.HeaderCell(0, 0, "Kiekis"), OcrTableFixtures.HeaderCell(0, 1, "Suma be PVM"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "999,99")) // deliberately different — must never be applied
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.NotAttempted, result.Outcome);
        Assert.Equal(100.00m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- No usable table at all: no crash, no change ----------

    [Fact]
    public void NoTables_NoChangeNoCrash()
    {
        var response = OcrFixtures.Response(
            OcrFixtures.Cur("500,00", 500.00), OcrFixtures.Cur("0", 0), OcrFixtures.Cur("500,00", 500.00),
            OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("600,00", 600.00), OcrFixtures.Cur("600,00", 600.00)));

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.NotAttempted, result.Outcome);
        Assert.Equal(600.00m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- A candidate table exists, matches vocabulary, but its own rows do not reconcile — never applied ----------

    [Fact]
    public void TableDoesNotReconcile_NeverApplied()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("500,00", 500.00), OcrFixtures.Cur("0", 0), OcrFixtures.Cur("500,00", 500.00),
            new[] { OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("600,00", 600.00), OcrFixtures.Cur("600,00", 600.00)) },
            new[]
            {
                OcrTableFixtures.Table(2, 2,
                    OcrTableFixtures.HeaderCell(0, 0, "Kiekis"), OcrTableFixtures.HeaderCell(0, 1, "Suma be PVM"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "550,00")) // reconciles with neither 500 nor 600
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.NotReconciling, result.Outcome);
        Assert.Equal(600.00m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- A table with unrecognised vocabulary (no language in Etapas 3's list) is ignored, not guessed ----------

    [Fact]
    public void TableWithUnrecognisedVocabulary_Ignored()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("500,00", 500.00), OcrFixtures.Cur("0", 0), OcrFixtures.Cur("500,00", 500.00),
            new[] { OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("600,00", 600.00), OcrFixtures.Cur("600,00", 600.00)) },
            new[]
            {
                OcrTableFixtures.Table(2, 2,
                    OcrTableFixtures.HeaderCell(0, 0, "Xyzzy"), OcrTableFixtures.HeaderCell(0, 1, "Plugh"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "500,00"))
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.NotAttempted, result.Outcome);
        Assert.Equal(600.00m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- Row count mismatch: never invent or drop a line ----------

    [Fact]
    public void TableRowCountDoesNotMatchLineCount_NeverApplied()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("500,00", 500.00), OcrFixtures.Cur("0", 0), OcrFixtures.Cur("500,00", 500.00),
            new[] { OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("600,00", 600.00), OcrFixtures.Cur("600,00", 600.00)) },
            new[]
            {
                OcrTableFixtures.Table(3, 2,
                    OcrTableFixtures.HeaderCell(0, 0, "Kiekis"), OcrTableFixtures.HeaderCell(0, 1, "Suma be PVM"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "250,00"),
                    OcrTableFixtures.Cell(2, 0, "1"), OcrTableFixtures.Cell(2, 1, "250,00")) // 2 data rows, only 1 Items line
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.NotEqual(TableRepairOutcome.Repaired, result.Outcome);
        Assert.Equal(600.00m, Assert.Single(dto.Lines).AmountExclVat);
    }

    // ---------- Only a gross column exists (EGO's exact shape) — never treated as net (documented limitation) ----------

    [Fact]
    public void OnlyGrossColumnExists_NeverTreatedAsNet_EgoLimitation()
    {
        var response = OcrTableFixtures.ResponseWithTables(
            OcrFixtures.Cur("430,00", 430.00), OcrFixtures.Cur("90,30", 90.30), OcrFixtures.Cur("520,30", 520.30),
            new[] { OcrFixtures.Line("Maršrutas", OcrFixtures.Num("1", 1), OcrFixtures.Cur("430,00", 430.00), OcrFixtures.Cur("520,30", 520.30)) }, // wrong: gross as net
            new[]
            {
                OcrTableFixtures.Table(2, 4,
                    OcrTableFixtures.HeaderCell(0, 0, "Kiekis"), OcrTableFixtures.HeaderCell(0, 1, "Kaina, EUR"),
                    OcrTableFixtures.HeaderCell(0, 2, "PVM"), OcrTableFixtures.HeaderCell(0, 3, "Suma su PVM"),
                    OcrTableFixtures.Cell(1, 0, "1"), OcrTableFixtures.Cell(1, 1, "430.00"), OcrTableFixtures.Cell(1, 2, "21"), OcrTableFixtures.Cell(1, 3, "520,30"))
            });

        var dto = OcrFixtures.Dto(response);
        var result = TableLineRepair.Repair(OcrTableFixtures.AnalyzeResult(response), dto);

        Assert.Equal(TableRepairOutcome.NotAttempted, result.Outcome); // no net/generic column found — never guesses from gross
        Assert.Equal(520.30m, Assert.Single(dto.Lines).AmountExclVat); // left as Items had it (still wrong, but not silently changed)
    }
}

/// <summary>Corpus-gated: apply the repair against every real Etapas 3 S1 corpus document; must never crash, and
/// must never move a document that already reconciles.</summary>
public class TableLineRepairCorpusTests
{
    [CorpusFact]
    public void Corpus_RepairNeverCrashes_AndNeverTouchesAnAlreadyReconcilingDocument()
    {
        foreach (var file in Directory.GetFiles(CorpusFactAttribute.Dir, "*.json"))
        {
            var raw = File.ReadAllText(file);
            var dto = OcrFixtures.Dto(raw);
            var before = dto.Lines.Select(l => l.AmountExclVat).ToList();
            var analyzeResult = OcrTableFixtures.AnalyzeResult(raw);

            var currentNets = dto.Lines.Select(l => (decimal?)l.AmountExclVat).ToList();
            var reconciled = En16931TotalsValidator.Validate(new En16931TotalsInput
            {
                SumOfLineNet = dto.AmountExclVat,
                LineNetAmounts = currentNets
            }).PassedRules.Contains(En16931TotalsValidator.BrCo10);

            var result = TableLineRepair.Repair(analyzeResult, dto);

            if (reconciled)
            {
                Assert.Equal(TableRepairOutcome.NotAttempted, result.Outcome);
                Assert.Equal(before, dto.Lines.Select(l => l.AmountExclVat).ToList());
            }
        }
    }
}
