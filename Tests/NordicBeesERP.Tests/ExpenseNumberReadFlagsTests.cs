using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S7(b) — <see cref="ExpenseService.RecomputeNumberReadFlags"/>, the one helper the OCR paths use
/// (D-041): NUMBER_MISREAD and NUMBER_AMBIGUOUS are both review, computed from the final DTO values; a value a human
/// changed after OCR is resolved. Values are never replaced. Pure tests.
/// </summary>
public class ExpenseNumberReadFlagsTests
{
    private static OcrResultDto Dto(string response) => OcrFixtures.Dto(response);

    private static List<string> Flags(OcrResultDto dto, params string[] existing)
    {
        var flags = existing.ToList();
        ExpenseService.RecomputeNumberReadFlags(flags, dto);
        return flags;
    }

    private static string CleanResponse() => OcrFixtures.Response(
        OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
        OcrFixtures.Line("A", OcrFixtures.Num("2,00", 2), OcrFixtures.Cur("50,00", 50), OcrFixtures.Cur("100,00", 100)));

    [Fact]
    public void Asf0021438_BothFlags_MisreadAndAmbiguous()
    {
        var flags = Flags(Dto(OcrFixtures.Asf0021438()));

        Assert.Contains(OcrFlag.NumberMisread, flags);
        Assert.Contains(OcrFlag.NumberAmbiguous, flags);
    }

    [Fact]
    public void Asf0021438_ValuesAreNeverReplaced()
    {
        var dto = Dto(OcrFixtures.Asf0021438());

        Flags(dto);

        Assert.Equal(3m, dto.Lines[0].Quantity);        // still Azure's 3, not the candidate 3888
        Assert.Equal(9000m, dto.Lines[1].Quantity);     // still Azure's 9000, not 9
        Assert.Equal(972.00m, dto.Lines[0].AmountExclVat);
        Assert.Equal(934.22m, dto.AmountExclVat);
    }

    [Fact]
    public void CleanDocument_NoFlags()
    {
        Assert.Empty(Flags(Dto(CleanResponse())));
    }

    [Fact]
    public void StaleOwnedFlags_AreDropped_OtherFlagsKept_InOrder()
    {
        var flags = Flags(Dto(CleanResponse()), OcrFlag.NumberMisread, OcrFlag.VendorNotFound, OcrFlag.NumberAmbiguous, OcrFlag.Duplicate);

        Assert.Equal(new[] { OcrFlag.VendorNotFound, OcrFlag.Duplicate }, flags);
    }

    [Fact]
    public void OnlyMisread_OnlyAmbiguous_EachFlagAloneWhenTheOtherIsAbsent()
    {
        var misread = Dto(OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("A", null, OcrFixtures.Cur("0,115", 115), null)));
        var ambiguous = Dto(OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("A", OcrFixtures.Num("1,000", 1), null, null)));

        Assert.Equal(new[] { OcrFlag.NumberMisread }, Flags(misread));
        Assert.Equal(new[] { OcrFlag.NumberAmbiguous }, Flags(ambiguous));
    }

    // ---------- a human editing the value is the resolution ----------

    [Fact]
    public void HumanCorrectsTheMisreadQuantity_FlagDropped_AmbiguousStays()
    {
        var dto = Dto(OcrFixtures.Asf0021438());
        dto.Lines[0].Quantity = 3888m;

        var flags = Flags(dto);

        Assert.DoesNotContain(OcrFlag.NumberMisread, flags);
        Assert.Contains(OcrFlag.NumberAmbiguous, flags);
    }

    [Fact]
    public void HumanSettlesTheAmbiguousQuantity_FlagDropped()
    {
        var dto = Dto(OcrFixtures.Asf0021438());
        dto.Lines[0].Quantity = 3888m;
        dto.Lines[1].Quantity = 9m;

        Assert.Empty(Flags(dto));
    }

    [Fact]
    public void AnyHumanValue_IsTheResolution_EvenOneThatIsNoCandidate()
    {
        // the person looked at the document and typed what they decided; detection only judges Azure's own reading
        var dto = Dto(OcrFixtures.Asf0021438());
        dto.Lines[0].Quantity = 5m;
        dto.Lines[1].Quantity = 10m;

        Assert.Empty(Flags(dto));
    }

    [Fact]
    public void UnchangedAmbiguousValue_StaysFlagged()
    {
        var dto = Dto(OcrFixtures.Asf0021438());
        dto.Lines[0].Quantity = 3888m;
        // line 2 untouched

        Assert.Contains(OcrFlag.NumberAmbiguous, Flags(dto));
    }

    [Fact]
    public void DeletedLine_LeavesNoFlag()
    {
        var dto = Dto(OcrFixtures.Asf0021438());
        dto.Lines.Clear();

        Assert.Empty(Flags(dto));
    }

    [Fact]
    public void HeaderTotalMisread_FlagsUntilTheHumanChangesIt()
    {
        var dto = Dto(OcrFixtures.Response(OcrFixtures.Cur("934,22", 934.22), OcrFixtures.Cur("196,18", 196.18), OcrFixtures.Cur("1 130,40", 130.4)));

        Assert.Contains(OcrFlag.NumberMisread, Flags(dto));

        dto.AmountInclVat = 1130.40m;

        Assert.Empty(Flags(dto));
    }

    [Fact]
    public void LineUnitPriceMisread_CorpusFuelPrice_Flags_AndCorrectionResolves()
    {
        var dto = Dto(OcrFixtures.Response(OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("21,00", 21), OcrFixtures.Cur("121,00", 121),
            OcrFixtures.Line("Kuras", OcrFixtures.Num("1600,00", 1600), OcrFixtures.Cur("0,115", 115), OcrFixtures.Cur("184,00", 184))));

        Assert.Contains(OcrFlag.NumberMisread, Flags(dto));

        dto.Lines[0].UnitPrice = 0.115m;

        Assert.Empty(Flags(dto));
    }

    [Fact]
    public void ADtoWithoutReads_ManualOrLegacy_HasNoFlags_AndDropsStaleOnes()
    {
        var dto = new OcrResultDto { AmountExclVat = 100m, VatAmount = 21m, AmountInclVat = 121m };
        dto.Lines.Add(new OcrLineDto { Quantity = 9000m, UnitPrice = 1m, AmountExclVat = 100m });

        Assert.Empty(Flags(dto, OcrFlag.NumberMisread));
    }

    [Fact]
    public void NegativeCreditNote_NoFlags()
    {
        var dto = Dto(OcrFixtures.Response(OcrFixtures.Cur("-100,00", -100), OcrFixtures.Cur("-21,00", -21), OcrFixtures.Cur("-1 210,00", -1210),
            OcrFixtures.Line("Grąžinimas", OcrFixtures.Num("-1,00", -1), OcrFixtures.Cur("100,00", 100), OcrFixtures.Cur("-100,00", -100))));

        Assert.Empty(Flags(dto));
    }
}
