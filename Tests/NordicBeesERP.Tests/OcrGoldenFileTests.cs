using NordicBeesERP.Tests.Validation;
using VerifyXunit;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 3, S2 (PLAN-ETAPAS3.md §7.2, §8.1 S2): golden-file regression on the normalised
/// <see cref="OcrResultDto"/> projection (<see cref="OcrFixtures.Snapshot"/>), never the raw Azure JSON — a red diff
/// here means "the extraction/normalisation logic changed behaviour on a real document", the signal Etapas 4 builds
/// on. Synthetic-fixture snapshots (this file) live in the repo; the corpus-derived ones live outside git next to
/// the corpus itself (<see cref="CorpusFactAttribute.Dir"/>/golden/) because the corpus holds personal data
/// (D-038 Q9, D-041) — same reasoning as <see cref="CorpusFactAttribute"/>.
/// </summary>
public class OcrGoldenFileTests
{
    /// <summary>The committed baseline: ASF0021438 (PLAN-ETAPAS1 §3.1) run through the same header/line reads
    /// <c>ExpenseOcrService.ProcessAsync</c> uses. A red diff on this test means the pure-extraction path itself
    /// changed — this is the test the smoke test below proves would actually catch a wrong extraction.</summary>
    [Fact]
    public Task Asf0021438_SnapshotMatchesKnownGood()
    {
        var snapshot = OcrFixtures.SnapshotOf(OcrFixtures.Asf0021438());
        return Verifier.Verify(snapshot);
    }

    /// <summary>
    /// The harness's own smoke test (PLAN-ETAPAS3.md §8.2 S2: "one smoke test proving a deliberately wrong
    /// extraction produces a red diff"). This test's own committed `.verified.txt` holds the CORRECT ASF0021438
    /// snapshot (identical in substance to <see cref="Asf0021438_SnapshotMatchesKnownGood"/>'s, but its own
    /// dedicated file — two tests sharing one Verify file prefix proved order-dependent/flaky under xUnit, so this
    /// avoids that instead of suppressing Verify's own uniqueness check). Line 1's net is mutated here from the
    /// correct 972,00 to a wrong value; verifying it against that committed-correct file must throw, proving the
    /// golden-file mechanism itself would catch a real regression. Never "accepts" this mismatch.
    /// </summary>
    [Fact]
    public async Task SmokeTest_WrongExtraction_ProducesARedDiff()
    {
        var wrongJson = OcrFixtures.Response(
            OcrFixtures.Cur("934,22", 934.22), OcrFixtures.Cur("196,18", 196.18), OcrFixtures.Cur("1 130,40", 1130.40),
            OcrFixtures.Line("Kuras A", OcrFixtures.Num("3 888,000", 3), OcrFixtures.Cur("0,2066", 0.2066), OcrFixtures.Cur("972,00", 999.99)), // wrong: 999.99, not 972.00
            OcrFixtures.Line("Kuras B", OcrFixtures.Num("9,000", 9000), OcrFixtures.Cur("14,5456", 14.5456), OcrFixtures.Cur("158,40", 158.40)));
        var wrongSnapshot = OcrFixtures.SnapshotOf(wrongJson);

        await Assert.ThrowsAnyAsync<Exception>(() => Verifier.Verify(wrongSnapshot).ToTask());
    }

    /// <summary>
    /// One snapshot per corpus document, stored outside git next to the corpus (personal data). Skipped entirely
    /// when the corpus is absent (<see cref="CorpusTheoryAttribute"/>). A red diff on an already-labelled document
    /// is a real regression; a red diff on one not yet labelled is new information (PLAN-ETAPAS3.md §7.2 point 3 —
    /// the received file must never be silently accepted to contradict a human label). A <see cref="Theory"/>, not
    /// one looping <see cref="Fact"/>, so one document's mismatch does not stop the rest from being checked/baselined.
    /// </summary>
    [CorpusTheory]
    [MemberData(nameof(CorpusFiles))]
    public async Task Corpus_DocumentSnapshotIsStable(string file)
    {
        var goldenDir = Path.Combine(CorpusFactAttribute.Dir, "golden");
        Directory.CreateDirectory(goldenDir);
        var hash = Path.GetFileNameWithoutExtension(file);
        var snapshot = OcrFixtures.SnapshotOf(File.ReadAllText(file));
        await Verifier.Verify(snapshot)
            .UseDirectory(goldenDir)
            .UseFileName(hash);
    }

    public static IEnumerable<object[]> CorpusFiles() =>
        Directory.Exists(CorpusFactAttribute.Dir)
            ? Directory.GetFiles(CorpusFactAttribute.Dir, "*.json").OrderBy(f => f).Select(f => new object[] { f })
            : new[] { new object[] { "" } }; // xUnit needs >=1 row even when the corpus (and thus CorpusTheory's Skip) makes it irrelevant
}

/// <summary>Theory counterpart of <see cref="CorpusFactAttribute"/> — same skip condition, for per-document test cases.</summary>
public sealed class CorpusTheoryAttribute : TheoryAttribute
{
    public CorpusTheoryAttribute()
    {
        if (!Directory.Exists(CorpusFactAttribute.Dir) || !File.Exists(Path.Combine(CorpusFactAttribute.Dir, "own-company.txt")) || Directory.GetFiles(CorpusFactAttribute.Dir, "*.json").Length == 0)
            Skip = "Azure corpus (~/NordicBeesERP-corpus with own-company.txt) not present";
    }
}
