using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 3 S4 (PLAN-ETAPAS3.md §5, §8.1 S4, D-026, D-046 OQ-4/OQ-5): ZERO_VAT / ZERO_VAT_NO_BASIS through
/// <see cref="ExpenseOcrService.ProcessAsync"/> itself — the wiring, not just <c>ZeroVatFormulationExtractor</c> in
/// isolation (that class's own behaviour is <see cref="Validation.ZeroVatFormulationExtractorTests"/>).
/// </summary>
public class ExpenseOcrServiceZeroVatTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseOcrServiceZeroVatTests(DbTestFixture fixture) => _fixture = fixture;

    private Task<OcrResultDto> ProcessAsync(string json) =>
        new RecordedAzureOcrService(json, _fixture.Factory).ProcessAsync("", "fixture.pdf");

    // ---------- Today's real rows are all UNCONFIRMED: behaviour must be byte-for-byte the same as before S4 ----------

    [Fact]
    public async Task ZeroVat_NoFormulationText_StaysPlainZeroVat_NoNewFlag()
    {
        var json = OcrZeroVatFixtures.Response(
            OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("100,00", 100),
            "Lithuania", "Sąskaita už prekes, jokios PVM išimties nuorodos.",
            OcrFixtures.Line("Prekė", OcrFixtures.Num("1", 1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("100,00", 100)));

        var result = await ProcessAsync(json);

        Assert.Contains(OcrFlag.ZeroVat, result.Flags);
        Assert.DoesNotContain(OcrFlag.ZeroVatNoBasis, result.Flags); // LT is UNCONFIRMED today — no new flag yet
    }

    [Fact]
    public async Task ZeroVat_Q009RealFormulationPresent_StillPlainZeroVat_NotClosed_BecauseLtIsUnconfirmed()
    {
        // The real staging example (Q-009): a formulation IS present and WOULD close the flag once LT is confirmed —
        // but today it must change NOTHING (D-046: "nieko nesuvelnina, kol nepatvirtinta").
        var json = OcrZeroVatFixtures.Response(
            OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("100,00", 100),
            "Lithuania", "Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5.",
            OcrFixtures.Line("Paslauga", OcrFixtures.Num("1", 1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("100,00", 100)));

        var result = await ProcessAsync(json);

        Assert.Contains(OcrFlag.ZeroVat, result.Flags); // NOT closed — LT is still UNCONFIRMED
        Assert.DoesNotContain(OcrFlag.ZeroVatNoBasis, result.Flags);
    }

    [Fact]
    public async Task ZeroVat_UnlistedCountry_StaysPlainZeroVat()
    {
        var json = OcrZeroVatFixtures.Response(
            OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("100,00", 100),
            "Germany", "Steuerfreie innergemeinschaftliche Lieferung", // DE not in the list yet, D-046 OQ-5
            OcrFixtures.Line("Ware", OcrFixtures.Num("1", 1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("100,00", 100)));

        var result = await ProcessAsync(json);

        Assert.Contains(OcrFlag.ZeroVat, result.Flags);
        Assert.DoesNotContain(OcrFlag.ZeroVatNoBasis, result.Flags);
    }

    // ---------- ULAK exemption (FROZEN.md §4): VAT there is always 6%, never 0% — structural, not special-cased ----------

    [Fact]
    public async Task NonZeroVatRate_NeverGetsZeroVatOrNoBasis_RegardlessOfFormulationText_UlakShape()
    {
        // 6% VAT (ULAK's real rate), derived from header VatAmount/AmountExclVat exactly as ProcessAsync always has —
        // no line TaxRate given, so this exercises the header-ratio fallback the same way a real ULAK invoice would.
        // Document text even contains a formulation pattern, to prove the VatRate==0 gate — not a ULAK-aware
        // branch — is what keeps this check from ever running.
        var json = OcrZeroVatFixtures.Response(
            OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("6,00", 6), OcrFixtures.Cur("106,00", 106),
            "Lithuania", "Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5.",
            OcrFixtures.Line("Bičių medus", OcrFixtures.Num("1", 1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("100,00", 100)));

        var result = await ProcessAsync(json);

        Assert.Equal(6m, result.VatRate);
        Assert.DoesNotContain(OcrFlag.ZeroVat, result.Flags);
        Assert.DoesNotContain(OcrFlag.ZeroVatNoBasis, result.Flags);
    }

    [Fact]
    public async Task ZeroAmount_NeverFlagged_HasReviewFlagsOwnGuard()
    {
        // AmountInclVat == 0: the ZERO_VAT gate's own second condition (result.AmountInclVat > 0) — a 0/0 document
        // (MISSING_MONEY_FIELD's own territory) must not also pick up ZERO_VAT.
        var json = OcrZeroVatFixtures.Response(
            OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("0,00", 0), OcrFixtures.Cur("0,00", 0),
            "Lithuania", "PVM5", Array.Empty<object>());

        var result = await ProcessAsync(json);

        Assert.DoesNotContain(OcrFlag.ZeroVat, result.Flags);
        Assert.DoesNotContain(OcrFlag.ZeroVatNoBasis, result.Flags);
    }
}
