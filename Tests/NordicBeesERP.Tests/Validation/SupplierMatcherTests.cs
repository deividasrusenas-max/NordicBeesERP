using NordicBeesERP.Services.Validation;
using Xunit;

namespace NordicBeesERP.Tests.Validation;

/// <summary>
/// OCR Etapas 2 S2b (PLAN-ETAPAS2 §1.1–§1.3, §7.2; D-017, D-044, D-045): the pure supplier matcher. Automatic assignment only
/// when exactly one eligible partner carries the strongest evidence and nothing contradicts it; everything else is loud.
/// </summary>
public class SupplierMatcherTests
{
    private const string LtIban = "LT121000011101001000";
    private const string DeIban = "DE89370400440532013000";

    private static SupplierCandidate P(int id, string name, string? vat = null, string? code = null,
        string? country = "LT", bool active = true, bool supplier = true, params string[] ibans) =>
        new(id, name, vat, code, country, ibans, active, supplier);

    private static SupplierDocument Doc(string? name = null, string? vat = null, string? code = null, string? iban = null,
        string? country = null) => new(name, vat, code, iban, country);

    private static SupplierMatch Match(SupplierDocument doc, params SupplierCandidate[] candidates) =>
        SupplierMatcher.Match(doc, candidates);

    private static void AssertAssigned(SupplierMatch m, int id, MatchTier tier)
    {
        Assert.Equal(MatchOutcome.Assigned, m.Outcome);
        Assert.Equal(id, m.PartnerId);
        Assert.Equal(tier, m.Tier);
        Assert.Equal(new[] { id }, m.CandidateIds);
    }

    private static void AssertNotAssigned(SupplierMatch m, MatchOutcome outcome, MatchReason reason)
    {
        Assert.Equal(outcome, m.Outcome);
        Assert.Null(m.PartnerId);
        Assert.Equal(reason, m.Reason);
    }

    // ---------------------------------------------------------------- tier 1: VAT

    [Theory]
    [InlineData("LT120252515")]
    [InlineData("lt 120252515")]
    [InlineData("LT-120.252.515")]
    [InlineData("  LT 120 252 515 ")]
    public void Vat_SpellingVariants_MatchOneStoredPartner(string docVat)
    {
        var m = Match(Doc(vat: docVat), P(1, "Other", vat: "LT999999999"), P(2, "Artea", vat: "LT120252515"));
        AssertAssigned(m, 2, MatchTier.Vat);
    }

    [Fact]
    public void Vat_StoredValueWithSeparators_Matches()
    {
        AssertAssigned(Match(Doc(vat: "LT120252515"), P(7, "Artea", vat: "LT 120-252-515")), 7, MatchTier.Vat);
    }

    [Fact]
    public void Vat_PrefixLessDocument_WithoutACountry_DoesNotMatchAPrefixedStoredCode_NoLtAssumption()
    {
        var m = Match(Doc(vat: "120252515"), P(1, "Artea", vat: "LT120252515"));
        Assert.Equal(MatchOutcome.NotFound, m.Outcome);
    }

    [Fact]
    public void Vat_PrefixLessDocument_WithAKnownCountry_Matches()
    {
        AssertAssigned(Match(Doc(vat: "120252515", country: "LT"), P(1, "Artea", vat: "LT120252515")), 1, MatchTier.Vat);
    }

    [Fact]
    public void Vat_PrefixLessDocument_MatchesOnlyAnIdenticalPrefixLessStoredValue()
    {
        AssertAssigned(Match(Doc(vat: "120 252 515"), P(1, "Old partner", vat: "120252515")), 1, MatchTier.Vat);
    }

    [Fact]
    public void Vat_PrefixLessStoredValue_IsNotGivenTheDocumentsPrefix()
    {
        // D-045: the stored side never gets a hint — a prefix-less partner is fixed in the master-data cleanup
        var m = Match(Doc(vat: "LT120252515", country: "LT"), P(1, "Old partner", vat: "120252515"));
        Assert.Equal(MatchOutcome.NotFound, m.Outcome);
    }

    [Fact]
    public void Vat_CompanyCodeAndVatAreNeverDerivedFromEachOther_Artea()
    {
        // company code 112025254, VAT LT120252515. A document that (wrongly) carries "LT" + the company code as VAT,
        // or the VAT digits as company code, must not find the partner
        var artea = P(1, "Artea", vat: "LT120252515", code: "112025254");
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(vat: "LT112025254"), artea).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(code: "120252515"), artea).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(vat: "112025254", country: "LT"), artea).Outcome);
    }

    // ---------------------------------------------------------------- empty identifiers

    [Fact]
    public void EmptyIdentifiers_NeverMatch_ThePartner336Case()
    {
        var blankPartners = new[]
        {
            P(336, "Antanas Auglys", vat: "", code: ""),
            P(337, "Blank", vat: null, code: null),
            P(338, "Spaces", vat: "  ", code: "  "),
        };
        foreach (var doc in new[]
        {
            Doc(),
            Doc(vat: "", code: "", name: "", iban: ""),
            Doc(vat: "   ", code: "-", name: "   "),
            Doc(vat: null, name: "UAB")           // nothing left after legal-form stripping
        })
        {
            var m = SupplierMatcher.Match(doc, blankPartners);
            Assert.Equal(MatchOutcome.NotFound, m.Outcome);
            Assert.Null(m.PartnerId);
            Assert.Empty(m.CandidateIds);
        }
    }

    [Fact]
    public void NoCandidates_IsNotFound()
    {
        Assert.Equal(MatchOutcome.NotFound, SupplierMatcher.Match(Doc(name: "Rotoma", vat: "LT1"), Array.Empty<SupplierCandidate>()).Outcome);
    }

    // ---------------------------------------------------------------- tier 2: company code

    [Fact]
    public void CompanyCode_WithoutVat_AssignsThePartner()
    {
        AssertAssigned(Match(Doc(code: "112 025 254"), P(1, "Artea", code: "112025254")), 1, MatchTier.CompanyCode);
    }

    [Fact]
    public void VatAndCompanyCode_Agreeing_AssignAtTheVatTier()
    {
        AssertAssigned(Match(Doc(vat: "LT120252515", code: "112025254"), P(1, "Artea", vat: "LT120252515", code: "112025254")), 1, MatchTier.Vat);
    }

    [Fact]
    public void VatMatch_PartnerHasNoCompanyCode_StillAssigned()
    {
        AssertAssigned(Match(Doc(vat: "LT120252515", code: "112025254"), P(1, "Artea", vat: "LT120252515")), 1, MatchTier.Vat);
    }

    // ---------------------------------------------------------------- contradiction rule (PLAN §1.3)

    [Fact]
    public void Contradiction_VatMatches_CompanyCodeDiffers_IsSuggestedNotAssigned()
    {
        var m = Match(Doc(vat: "LT120252515", code: "999999999"), P(1, "Artea", vat: "LT120252515", code: "112025254"));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.ConflictingIdentifier);
        Assert.Equal(new[] { 1 }, m.CandidateIds);
    }

    [Fact]
    public void Contradiction_CompanyCodeMatches_VatDiffers_IsSuggestedNotAssigned()
    {
        var m = Match(Doc(vat: "LT111111111", code: "112025254"), P(1, "Artea", vat: "LT120252515", code: "112025254"));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.ConflictingIdentifier);
    }

    [Fact]
    public void Contradiction_VatPointsAtOnePartner_CodeAtAnother_IsAmbiguous()
    {
        var m = Match(Doc(vat: "LT120252515", code: "112025254"),
            P(1, "A", vat: "LT120252515"), P(2, "B", code: "112025254"));
        AssertNotAssigned(m, MatchOutcome.Ambiguous, MatchReason.StrongIdentifiersDisagree);
        Assert.Equal(new[] { 1, 2 }, m.CandidateIds.OrderBy(i => i));
    }

    [Fact]
    public void Contradiction_SameNameDifferentVat_IsNeverAssigned_PlanSection1_3()
    {
        var m = Match(Doc(name: "UAB Rotoma", vat: "LT222222222"), P(1, "UAB Rotoma", vat: "LT111111111"));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.ConflictingIdentifier);
    }

    [Fact]
    public void Contradiction_SameNameDifferentCompanyCode_IsNeverAssigned()
    {
        var m = Match(Doc(name: "UAB Rotoma", code: "222222222"), P(1, "UAB Rotoma", code: "111111111"));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.ConflictingIdentifier);
    }

    // ---------------------------------------------------------------- duplicates and eligibility

    [Fact]
    public void DuplicatePartners_SameVat_AreAmbiguous_NeverAPick_Rotoma369_381()
    {
        var m = Match(Doc(vat: "LT100001049516"), P(369, "UAB Rotoma", vat: "LT100001049516"), P(381, "UAB Rotoma", vat: "LT100001049516"));
        AssertNotAssigned(m, MatchOutcome.Ambiguous, MatchReason.DuplicatePartners);
        Assert.Equal(new[] { 369, 381 }, m.CandidateIds.OrderBy(i => i));
    }

    [Fact]
    public void DuplicatePartners_SameCompanyCode_AreAmbiguous()
    {
        var m = Match(Doc(code: "112025254"), P(1, "A", code: "112025254"), P(2, "B", code: "112025254"));
        AssertNotAssigned(m, MatchOutcome.Ambiguous, MatchReason.DuplicatePartners);
    }

    [Fact]
    public void IndividualBeekeepers_WithIdenticalNames_AreAmbiguous_NeverAnAutoPick()
    {
        var m = Match(Doc(name: "Bernotas Jonas"),
            P(79, "Bernotas Jonas"), P(328, "BERNOTAS JONAS"));
        AssertNotAssigned(m, MatchOutcome.Ambiguous, MatchReason.DuplicatePartners);
        Assert.Equal(new[] { 79, 328 }, m.CandidateIds.OrderBy(i => i));
    }

    [Fact]
    public void DuplicateVat_OneInactive_IsAssignedToTheActivePartner()
    {
        var m = Match(Doc(vat: "LT120252515"), P(1, "Old", vat: "LT120252515", active: false), P(2, "Current", vat: "LT120252515"));
        AssertAssigned(m, 2, MatchTier.Vat);
    }

    [Fact]
    public void InactivePartner_IsOnlySuggested_Q7()
    {
        var m = Match(Doc(vat: "LT120252515"), P(1, "Old", vat: "LT120252515", active: false));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.PartnerNotEligible);
        Assert.Equal(new[] { 1 }, m.CandidateIds);
    }

    [Fact]
    public void CustomerOnlyPartner_IsOnlySuggested_Q7()
    {
        var m = Match(Doc(vat: "LT120252515"), P(1, "Buyer", vat: "LT120252515", supplier: false));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.PartnerNotEligible);
    }

    [Fact]
    public void InactiveOrCustomerOnly_ByExactName_IsOnlySuggested()
    {
        AssertNotAssigned(Match(Doc(name: "Rotoma"), P(1, "Rotoma", active: false)), MatchOutcome.Suggested, MatchReason.PartnerNotEligible);
        AssertNotAssigned(Match(Doc(name: "Rotoma"), P(1, "Rotoma", supplier: false)), MatchOutcome.Suggested, MatchReason.PartnerNotEligible);
    }

    [Fact]
    public void InactiveOrCustomerOnly_ByCompanyCode_IsOnlySuggested()
    {
        AssertNotAssigned(Match(Doc(code: "112025254"), P(1, "X", code: "112025254", active: false)), MatchOutcome.Suggested, MatchReason.PartnerNotEligible);
    }

    // ---------------------------------------------------------------- tier 4: exact name (D-044 Q1)

    [Theory]
    [InlineData("UAB Rotoma")]
    [InlineData("uab rotoma")]
    [InlineData("  UAB   ROTOMA ")]
    public void ExactName_NoVatNoCode_AssignsTheUniquePartner(string docName)
    {
        AssertAssigned(Match(Doc(name: docName), P(1, "UAB Rotoma"), P(2, "UAB Other")), 1, MatchTier.Name);
    }

    [Fact]
    public void ExactName_FoldsDiacritics()
    {
        AssertAssigned(Match(Doc(name: "UAB Zukline"), P(1, "UAB Žūklinė")), 1, MatchTier.Name);
    }

    [Fact]
    public void ExactName_DocumentHasVat_PartnerHasNone_IsSuggested_NotAssigned()
    {
        // the partner-336 shape: a partner without identifiers must not absorb a document that carries one
        var m = Match(Doc(name: "UAB Rotoma", vat: "LT120252515"), P(1, "UAB Rotoma", vat: ""));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.PartnerLacksIdentifier);
    }

    [Fact]
    public void ExactName_DocumentHasCompanyCode_PartnerHasNone_IsSuggested_NotAssigned()
    {
        var m = Match(Doc(name: "UAB Rotoma", code: "112025254"), P(1, "UAB Rotoma"));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.PartnerLacksIdentifier);
    }

    [Fact]
    public void ExactName_VatIsStrongerThanName()
    {
        // the VAT points at partner 2, the name at partner 1: the VAT wins (tier 1) and nothing is assigned by name
        var m = Match(Doc(name: "UAB Rotoma", vat: "LT120252515"), P(1, "UAB Rotoma", vat: "LT120252515"), P(2, "UAB Other", vat: "LT999999999"));
        AssertAssigned(m, 1, MatchTier.Vat);
    }

    // ---------------------------------------------------------------- tier 4b: normalised name — suggest only

    [Theory]
    [InlineData("UAB Rotoma", "Rotoma, AB")]
    [InlineData("UAB „Rotoma“", "AB Rotoma")]
    [InlineData("Rotoma", "UAB ROTOMA")]
    public void NormalisedName_IsSuggestedNeverAssigned(string docName, string partnerName)
    {
        var m = Match(Doc(name: docName), P(1, partnerName));
        // "UAB Rotoma" vs "UAB ROTOMA" is exact-equal and may legitimately be assigned; every other pair is legal-form-only
        if (SupplierIdentityNormalizer.NameExact(docName) == SupplierIdentityNormalizer.NameExact(partnerName))
            AssertAssigned(m, 1, MatchTier.Name);
        else
        {
            AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.NormalizedNameOnly);
            Assert.Equal(MatchTier.NormalizedName, m.Tier);
        }
    }

    [Fact]
    public void NormalisedName_TwoPartners_IsAmbiguous()
    {
        var m = Match(Doc(name: "Rotoma"), P(1, "UAB Rotoma"), P(2, "AB Rotoma"));
        AssertNotAssigned(m, MatchOutcome.Ambiguous, MatchReason.DuplicatePartners);
        Assert.Equal(MatchTier.NormalizedName, m.Tier);
    }

    [Fact]
    public void NormalisedName_LegalFormOnly_HasNothingToMatchOn()
    {
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(name: "UAB"), P(1, "AB")).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(name: "Ir"), P(1, "&")).Outcome);
    }

    // ---------------------------------------------------------------- IBAN: check and suggest only (D-044 Q2, Q5)

    [Fact]
    public void Iban_Alone_IsSuggestedNeverAssigned()
    {
        var m = Match(Doc(iban: "lt12 1000 0111 0100 1000"), P(1, "Bank owner", ibans: LtIban), P(2, "Other", ibans: DeIban));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.IbanOnly);
        Assert.Equal(MatchTier.Iban, m.Tier);
        Assert.Equal(new[] { 1 }, m.CandidateIds);
    }

    [Fact]
    public void Iban_InvalidOrEmpty_IsNeverAKey()
    {
        var owner = P(1, "Bank owner", ibans: LtIban);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(iban: "LT121000011101001001"), owner).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(iban: ""), owner).Outcome);
        Assert.Equal(MatchOutcome.NotFound, Match(Doc(iban: "not an iban"), owner).Outcome);
    }

    [Fact]
    public void Iban_SharedByTwoPartners_IsSuggestedWithBoth_NeverAPick()
    {
        var m = Match(Doc(iban: LtIban), P(1, "A", ibans: LtIban), P(2, "B", ibans: LtIban));
        AssertNotAssigned(m, MatchOutcome.Suggested, MatchReason.IbanOnly);
        Assert.Equal(new[] { 1, 2 }, m.CandidateIds.OrderBy(i => i));
    }

    [Fact]
    public void AssignedPartner_ReportsWhetherTheDocumentIbanIsKnown()
    {
        var known = Match(Doc(vat: "LT120252515", iban: LtIban), P(1, "Artea", vat: "LT120252515", ibans: new[] { LtIban, DeIban }));
        AssertAssigned(known, 1, MatchTier.Vat);
        Assert.True(known.DocumentIbanKnown);
        Assert.Equal(2, known.PartnerKnownIbanCount);

        var fresh = Match(Doc(vat: "LT120252515", iban: DeIban), P(1, "Artea", vat: "LT120252515", ibans: LtIban));
        Assert.False(fresh.DocumentIbanKnown);
        Assert.Equal(1, fresh.PartnerKnownIbanCount);
    }

    [Fact]
    public void AssignedPartner_WithNoKnownIbans_ReportsZero_S4RaisesNoFlag()
    {
        var m = Match(Doc(vat: "LT120252515", iban: LtIban), P(1, "Artea", vat: "LT120252515"));
        AssertAssigned(m, 1, MatchTier.Vat);
        Assert.False(m.DocumentIbanKnown);
        Assert.Equal(0, m.PartnerKnownIbanCount);
    }

    [Fact]
    public void AssignedPartner_DocumentWithoutAValidIban_ReportsUnknown()
    {
        var m = Match(Doc(vat: "LT120252515", iban: "garbage"), P(1, "Artea", vat: "LT120252515", ibans: LtIban));
        Assert.Null(m.DocumentIbanKnown);
    }

    [Fact]
    public void KnownIbans_InvalidStoredValues_AreIgnored()
    {
        var m = Match(Doc(vat: "LT120252515", iban: LtIban), P(1, "Artea", vat: "LT120252515", ibans: new[] { "junk", "", LtIban }));
        Assert.True(m.DocumentIbanKnown);
        Assert.Equal(1, m.PartnerKnownIbanCount);
    }

    [Fact]
    public void Iban_NeverOverridesAStrongerTier()
    {
        // VAT names partner 1; the IBAN belongs to partner 2 — the VAT decides, the IBAN is only reported as unknown for 1
        var m = Match(Doc(vat: "LT120252515", iban: DeIban), P(1, "A", vat: "LT120252515", ibans: LtIban), P(2, "B", ibans: DeIban));
        AssertAssigned(m, 1, MatchTier.Vat);
        Assert.False(m.DocumentIbanKnown);
    }

    // ---------------------------------------------------------------- nothing found / extension point

    [Fact]
    public void NothingMatches_IsNotFound()
    {
        var m = Match(Doc(name: "Unknown UAB Foo", vat: "LT555555555", code: "555555555", iban: DeIban), P(1, "Rotoma", vat: "LT120252515"));
        Assert.Equal(MatchOutcome.NotFound, m.Outcome);
        Assert.Equal(MatchTier.None, m.Tier);
        Assert.Equal(MatchReason.None, m.Reason);
    }

    [Fact]
    public void AliasTier_IsAnExtensionPointOnly_TheMatcherNeverProducesIt()
    {
        var everything = new[]
        {
            Match(Doc(vat: "LT120252515"), P(1, "A", vat: "LT120252515")),
            Match(Doc(name: "Rotoma"), P(1, "UAB Rotoma")),
            Match(Doc(iban: LtIban), P(1, "A", ibans: LtIban)),
            Match(Doc(name: "x"), P(1, "y")),
        };
        Assert.All(everything, m => Assert.NotEqual(MatchTier.Alias, m.Tier));
    }
}
