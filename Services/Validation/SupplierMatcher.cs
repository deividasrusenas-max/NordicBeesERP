namespace NordicBeesERP.Services.Validation;

/// <summary>One partner as the matcher sees it — a snapshot, no I/O (PLAN-ETAPAS2 §1.1).</summary>
/// <param name="KnownIbans">The partner's known bank accounts (raw or normalised; compared through <see cref="SupplierIdentityNormalizer.Iban"/>).</param>
/// <param name="IsSupplierRole"><c>is_supplier</c> or <c>is_expense_supplier</c> (D-044 Q7).</param>
public sealed record SupplierCandidate(
    int Id,
    string? Name,
    string? VatCode,
    string? CompanyCode,
    string? CountryCode,
    IReadOnlyList<string> KnownIbans,
    bool IsActive,
    bool IsSupplierRole)
{
    /// <summary>Only active partners with a supplier role may be assigned automatically (D-044 Q7); the rest are only ever suggested.</summary>
    public bool IsEligible => IsActive && IsSupplierRole;
}

/// <summary>The identifiers read from the document. <see cref="CountryHint"/> is the country known from the address / resolver, or null.</summary>
public sealed record SupplierDocument(string? Name, string? VatCode, string? CompanyCode, string? Iban, string? CountryHint);

public enum MatchOutcome
{
    /// <summary>One eligible partner, strong evidence, nothing contradicts — safe to assign.</summary>
    Assigned,
    /// <summary>Weaker evidence, or evidence that contradicts — shown to the human, never assigned.</summary>
    Suggested,
    /// <summary>Two or more partners tie at the deciding tier (or two strong identifiers point at different partners).</summary>
    Ambiguous,
    NotFound
}

public enum MatchTier
{
    None,
    Vat,
    CompanyCode,
    Name,
    NormalizedName,
    Iban,
    /// <summary>Extension point for S5 (learned aliases); the matcher never produces it in S2.</summary>
    Alias
}

public enum MatchReason
{
    None,
    /// <summary>Two eligible partners carry the same identifier / exact name.</summary>
    DuplicatePartners,
    /// <summary>The VAT code and the company code point at different partners.</summary>
    StrongIdentifiersDisagree,
    /// <summary>Document and partner both have a VAT code (or company code) and they differ.</summary>
    ConflictingIdentifier,
    /// <summary>Matched by name, the document carries a VAT / company code and the partner has none to compare.</summary>
    PartnerLacksIdentifier,
    /// <summary>The only matching partners are inactive or not suppliers.</summary>
    PartnerNotEligible,
    /// <summary>Equal only after legal forms / punctuation were stripped.</summary>
    NormalizedNameOnly,
    /// <summary>The document IBAN is known for the partner, nothing else matched (D-044 Q2).</summary>
    IbanOnly
}

/// <param name="PartnerId">Set only when <see cref="Outcome"/> is <see cref="MatchOutcome.Assigned"/>.</param>
/// <param name="CandidateIds">Every partner the deciding tier found (for Suggested / Ambiguous, and the assigned one for Assigned).</param>
/// <param name="DocumentIbanKnown">Null when the document has no valid IBAN or nothing was assigned; otherwise whether it is among the assigned partner's known IBANs.</param>
/// <param name="PartnerKnownIbanCount">Valid known IBANs of the assigned partner (0 when nothing was assigned) — S4 raises SUPPLIER_NEW_IBAN only when this is at least 1 (D-044 Q5).</param>
public sealed record SupplierMatch(
    MatchOutcome Outcome,
    int? PartnerId,
    MatchTier Tier,
    MatchReason Reason,
    IReadOnlyList<int> CandidateIds,
    bool? DocumentIbanKnown,
    int PartnerKnownIbanCount);

/// <summary>
/// The supplier cascade as one pure function (PLAN-ETAPAS2 §1, D-017, D-044, D-045). Automatic assignment only when
/// the strongest evidence points at exactly one eligible partner and no identifier both sides carry contradicts it;
/// everything else is loud (Suggested / Ambiguous / NotFound). An empty identifier never matches anything.
/// <para>Tiers: 1 VAT code, 2 company code, 4 exact name, 4b normalised name (suggest only), 3 IBAN (suggest only, and a check
/// on the assigned partner). Alias (tier 8) is an extension point for S5.</para>
/// </summary>
public static class SupplierMatcher
{
    public static SupplierMatch Match(SupplierDocument doc, IReadOnlyList<SupplierCandidate> candidates)
    {
        var docVat = SupplierIdentityNormalizer.Vat(doc.VatCode, doc.CountryHint).Normalized;
        var docCode = SupplierIdentityNormalizer.CompanyCode(doc.CompanyCode);
        var docNameExact = SupplierIdentityNormalizer.NameExact(doc.Name);
        var docNameNorm = SupplierIdentityNormalizer.NameNormalized(doc.Name);
        var docIban = SupplierIdentityNormalizer.Iban(doc.Iban);

        // Stored codes are compared as they are stored, after normalisation — no country hint on the stored side (D-045 Q8)
        string? StoredVat(SupplierCandidate c) => SupplierIdentityNormalizer.Vat(c.VatCode).Normalized;
        string? StoredCode(SupplierCandidate c) => SupplierIdentityNormalizer.CompanyCode(c.CompanyCode);

        var vatHits = docVat == null ? new List<SupplierCandidate>() : candidates.Where(c => StoredVat(c) == docVat).ToList();
        var codeHits = docCode == null ? new List<SupplierCandidate>() : candidates.Where(c => StoredCode(c) == docCode).ToList();

        // ---- tier 1: VAT code, then tier 2: company code
        var strong = Decide(MatchTier.Vat, vatHits, codeHits, docCode, StoredCode, doc, docIban);
        if (strong != null) return strong;
        strong = Decide(MatchTier.CompanyCode, codeHits, vatHits, docVat, StoredVat, doc, docIban);
        if (strong != null) return strong;

        // ---- tier 4: exact name
        if (docNameExact.Length > 0)
        {
            var exact = candidates.Where(c => SupplierIdentityNormalizer.NameExact(c.Name) == docNameExact).ToList();
            var result = DecideByName(exact, docVat, docCode, StoredVat, StoredCode, docIban);
            if (result != null) return result;
        }

        // ---- tier 4b: normalised name — suggestion only
        if (docNameNorm.Length > 0)
        {
            var norm = candidates.Where(c => SupplierIdentityNormalizer.NameNormalized(c.Name) == docNameNorm).ToList();
            if (norm.Count == 1)
                return Suggested(MatchTier.NormalizedName, MatchReason.NormalizedNameOnly, norm);
            if (norm.Count > 1)
                return new SupplierMatch(MatchOutcome.Ambiguous, null, MatchTier.NormalizedName, MatchReason.DuplicatePartners,
                    Ids(norm), null, 0);
        }

        // ---- tier 3: IBAN alone — suggest only (D-044 Q2)
        if (docIban != null)
        {
            var ibanHits = candidates.Where(c => KnownIbanSet(c).Contains(docIban)).ToList();
            if (ibanHits.Count > 0)
                return Suggested(MatchTier.Iban, MatchReason.IbanOnly, ibanHits);
        }

        return new SupplierMatch(MatchOutcome.NotFound, null, MatchTier.None, MatchReason.None, Array.Empty<int>(), null, 0);
    }

    // The strong tier: `hits` are the partners that share the tier's identifier; `otherHits` those that share the other strong identifier.
    private static SupplierMatch? Decide(MatchTier tier, List<SupplierCandidate> hits, List<SupplierCandidate> otherHits,
        string? docOther, Func<SupplierCandidate, string?> storedOther, SupplierDocument doc, string? docIban)
    {
        if (hits.Count == 0) return null;

        var eligible = hits.Where(c => c.IsEligible).ToList();
        if (eligible.Count == 0)
            return Suggested(tier, MatchReason.PartnerNotEligible, hits);
        if (eligible.Count > 1)
            return new SupplierMatch(MatchOutcome.Ambiguous, null, tier, MatchReason.DuplicatePartners, Ids(eligible), null, 0);

        var partner = eligible[0];

        // the other strong identifier points at a different eligible partner → the two identifiers disagree
        var conflicting = otherHits.Where(c => c.IsEligible && c.Id != partner.Id).ToList();
        if (conflicting.Count > 0)
            return new SupplierMatch(MatchOutcome.Ambiguous, null, tier, MatchReason.StrongIdentifiersDisagree,
                Ids(new[] { partner }.Concat(conflicting)), null, 0);

        // both sides carry the other strong identifier and it differs → contradiction (PLAN §1.3): suggest, never assign
        var partnerOther = storedOther(partner);
        if (docOther != null && partnerOther != null && docOther != partnerOther)
            return Suggested(tier, MatchReason.ConflictingIdentifier, new[] { partner });

        return Assigned(tier, partner, docIban);
    }

    private static SupplierMatch? DecideByName(List<SupplierCandidate> exact, string? docVat, string? docCode,
        Func<SupplierCandidate, string?> storedVat, Func<SupplierCandidate, string?> storedCode, string? docIban)
    {
        if (exact.Count == 0) return null;

        var eligible = exact.Where(c => c.IsEligible).ToList();
        if (eligible.Count == 0)
            return Suggested(MatchTier.Name, MatchReason.PartnerNotEligible, exact);
        if (eligible.Count > 1)
            return new SupplierMatch(MatchOutcome.Ambiguous, null, MatchTier.Name, MatchReason.DuplicatePartners, Ids(eligible), null, 0);

        var partner = eligible[0];

        // D-044 Q1: auto only when the document carries no VAT and no company code — otherwise a code either
        // contradicts the partner's or has nothing on the partner's side to agree with; both stay loud
        var pVat = storedVat(partner);
        var pCode = storedCode(partner);
        if ((docVat != null && pVat != null && docVat != pVat) || (docCode != null && pCode != null && docCode != pCode))
            return Suggested(MatchTier.Name, MatchReason.ConflictingIdentifier, new[] { partner });
        if ((docVat != null && pVat == null) || (docCode != null && pCode == null))
            return Suggested(MatchTier.Name, MatchReason.PartnerLacksIdentifier, new[] { partner });
        if (docVat != null || docCode != null)
        {
            // the document carries a code and the partner carries the same one — that would have matched at tier 1/2, so
            // reaching here means the codes are not comparable; stay loud
            return Suggested(MatchTier.Name, MatchReason.PartnerLacksIdentifier, new[] { partner });
        }

        return Assigned(MatchTier.Name, partner, docIban);
    }

    private static SupplierMatch Assigned(MatchTier tier, SupplierCandidate partner, string? docIban)
    {
        var known = KnownIbanSet(partner);
        bool? ibanKnown = docIban == null ? null : known.Contains(docIban);
        return new SupplierMatch(MatchOutcome.Assigned, partner.Id, tier, MatchReason.None, new[] { partner.Id }, ibanKnown, known.Count);
    }

    private static SupplierMatch Suggested(MatchTier tier, MatchReason reason, IEnumerable<SupplierCandidate> partners) =>
        new(MatchOutcome.Suggested, null, tier, reason, Ids(partners), null, 0);

    private static IReadOnlyList<int> Ids(IEnumerable<SupplierCandidate> partners) => partners.Select(p => p.Id).Distinct().ToList();

    private static HashSet<string> KnownIbanSet(SupplierCandidate c) =>
        c.KnownIbans.Select(SupplierIdentityNormalizer.Iban).Where(i => i != null).Select(i => i!).ToHashSet();
}
