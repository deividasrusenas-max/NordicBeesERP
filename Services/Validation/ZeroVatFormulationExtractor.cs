using System.Text.RegularExpressions;

namespace NordicBeesERP.Services.Validation;

/// <summary>Confirmation state of one <see cref="ZeroVatFormulationRow"/> — same meaning and mechanism as
/// <see cref="VatRateRowStatus"/> in <see cref="VatRateTable"/> (D-046: "tas pats modelis kaip PVM tarifų
/// lentelė").</summary>
public enum ZeroVatBasisStatus
{
    /// <summary>Patterns taken from research/general knowledge; NOT yet confirmed by the accountant (D-046 OQ-5, Q-009).</summary>
    Unconfirmed,
    /// <summary>Confirmed by the accountant as the real formulation(s) seen in practice for this country.</summary>
    Confirmed
}

/// <summary>One country's legal-basis search patterns for a 0 % VAT invoice (D-026).</summary>
public sealed record ZeroVatFormulationRow(string CountryCode, ZeroVatBasisStatus Status, string Source, IReadOnlyList<Regex> Patterns);

public enum ZeroVatCheckOutcome
{
    /// <summary>No pattern matched (or the country is not in the list at all, or the country's rows are still
    /// UNCONFIRMED) — today's (all-UNCONFIRMED) behaviour: plain ZERO_VAT, unchanged from before this class existed.</summary>
    NoBasisFound,
    /// <summary>A pattern matched, but that country's list is still UNCONFIRMED — today's behaviour is UNCHANGED
    /// (plain ZERO_VAT, same as <see cref="NoBasisFound"/>); this outcome exists only so the audit trail can show
    /// what text WOULD close the flag once the accountant confirms this country's list.</summary>
    BasisFoundButUnconfirmed,
    /// <summary>A CONFIRMED country's pattern matched — ZERO_VAT closes (becomes information, D-026's "vėliavėlė užsidaro").</summary>
    BasisConfirmedFound,
    /// <summary>The country's list IS CONFIRMED, but nothing matched — a real, actionable review reason (ZERO_VAT_NO_BASIS).</summary>
    ConfirmedNoBasisFound
}

public sealed record ZeroVatCheckResult(ZeroVatCheckOutcome Outcome, string? MatchedPattern, string? CountryCode);

/// <summary>
/// D-026's legal-basis formulation check for a 0 % VAT invoice (PLAN-ETAPAS3.md §5, D-046 OQ-4/OQ-5). Pure, no I/O —
/// reads the document's raw text (<c>analyzeResult.content</c>), the same string <see cref="SupplierCompanyCodeExtractor"/>
/// already reads instead of a typed Azure field.
/// <para>
/// <b>Every row is UNCONFIRMED.</b> Patterns come from RESEARCH-2026-09-25-reliability.md §3's own caveat ("Atvirkštinis
/// apmokestinimas" practical usage NOT confirmed), D-026's own examples (Art. 138, the "Reverse charge" family), and the
/// one real example found on staging (Q-009): „Finansinių paslaugų teikimas - PVM įstatymo 28 straipsnis, PVM5." — an
/// i.SAF PVMx classifier code plus a PVMĮ article reference. Country priority per D-046 OQ-5: LT first, then PL, RO, CZ,
/// ES (the real supplier base); DE/LV/EE/UA added only once real documents from them appear.
/// </para>
/// <para>
/// <b>How a row becomes CONFIRMED (data-only change, this file only) — same mechanism as <see cref="VatRateTable"/>.</b>
/// Change <see cref="ZeroVatBasisStatus.Unconfirmed"/> to <see cref="ZeroVatBasisStatus.Confirmed"/> for that country's
/// row (and update its <c>Source</c> comment with what the accountant confirmed), then update the tests that pin
/// "nothing confirmed yet". Behaviour: on an UNCONFIRMED row a match still leaves the invoice in review — nothing gets
/// looser until a human confirms a list (D-046: "nieko nesuvelnina, kol nepatvirtinta").
/// </para>
/// </summary>
public static class ZeroVatFormulationExtractor
{
    private const string D026Examples = "D-026 (Docs/ocr-rebuild/DECISIONS.md) — Directive 226(11)/(11a) style examples, not confirmed against real LT invoices";
    private const string Q009RealExample = "Q-009 (Docs/ocr-rebuild/OPEN-QUESTIONS.md) — the one real staging example (AB Artea, PVM įstatymo 28 str., i.SAF code PVM5); the general \"PVM įstatymo N straipsnis\" / \"PVMx\" shapes are inferred from this single example, not yet confirmed as the general LT pattern";
    private const string GeneralKnowledge = "author's general knowledge of the EU reverse-charge phrase family, NOT in the research — UNCONFIRMED, check with the accountant";

    public static readonly IReadOnlyList<ZeroVatFormulationRow> Rows = new[]
    {
        // LT first (D-046 OQ-5): i.SAF PVMx classifier codes (Q-009's own real example, "PVM5") and PVMĮ article
        // references ("PVM įstatymo 28 straipsnis"), plus D-026's own Directive-226-style examples.
        new ZeroVatFormulationRow("LT", ZeroVatBasisStatus.Unconfirmed, Q009RealExample, new Regex[]
        {
            new(@"\bPVM\s?\d\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),              // i.SAF classifier code, e.g. "PVM5"
            new(@"PVM\s+įstatymo\s+\d+\s+str", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), // "PVM įstatymo 28 straipsnis"
        }),
        new ZeroVatFormulationRow("LT", ZeroVatBasisStatus.Unconfirmed, D026Examples, new Regex[]
        {
            new(@"Art\.?\s*138\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),             // intra-Community supply, Directive 226(11)
            new(@"Atvirkštinis\s+apmokestinimas", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), // reverse charge — RESEARCH's own unverified guess
        }),
        new ZeroVatFormulationRow("PL", ZeroVatBasisStatus.Unconfirmed, GeneralKnowledge, new Regex[]
        {
            new(@"odwrotne\s+obciążenie", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),      // reverse charge (PL)
        }),
        new ZeroVatFormulationRow("RO", ZeroVatBasisStatus.Unconfirmed, GeneralKnowledge, new Regex[]
        {
            new(@"taxare\s+invers[aă]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),        // reverse charge (RO)
        }),
        new ZeroVatFormulationRow("CZ", ZeroVatBasisStatus.Unconfirmed, GeneralKnowledge, new Regex[]
        {
            new(@"přenesení\s+daňové\s+povinnosti", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), // reverse charge (CZ)
        }),
        new ZeroVatFormulationRow("ES", ZeroVatBasisStatus.Unconfirmed, GeneralKnowledge, new Regex[]
        {
            new(@"inversión\s+del\s+sujeto\s+pasivo", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), // reverse charge (ES)
        }),
    };

    /// <summary>True only when every row is <see cref="ZeroVatBasisStatus.Confirmed"/>. False today.</summary>
    public static bool AllRowsConfirmed => Rows.All(r => r.Status == ZeroVatBasisStatus.Confirmed);

    public static ZeroVatCheckResult Check(string? documentText, string? countryCode) => Check(documentText, countryCode, Rows);

    /// <summary><see cref="Check(string?, string?)"/> against a given row set (tests use it to exercise a CONFIRMED row).</summary>
    public static ZeroVatCheckResult Check(string? documentText, string? countryCode, IReadOnlyList<ZeroVatFormulationRow> rows)
    {
        var country = countryCode?.Trim().ToUpperInvariant();
        var candidateRows = string.IsNullOrEmpty(country) ? new List<ZeroVatFormulationRow>() : rows.Where(r => r.CountryCode == country).ToList();
        // "Confirmed" for a country only once EVERY row of it is — a country half-migrated mid-review must not
        // start driving real REVIEW status on the strength of only some of its rows (same caution as VatRateTable).
        var countryConfirmed = candidateRows.Count > 0 && candidateRows.All(r => r.Status == ZeroVatBasisStatus.Confirmed);

        if (!string.IsNullOrWhiteSpace(documentText))
        {
            foreach (var row in candidateRows)
            {
                foreach (var pattern in row.Patterns)
                {
                    var match = pattern.Match(documentText);
                    if (!match.Success) continue;
                    return new ZeroVatCheckResult(
                        row.Status == ZeroVatBasisStatus.Confirmed ? ZeroVatCheckOutcome.BasisConfirmedFound : ZeroVatCheckOutcome.BasisFoundButUnconfirmed,
                        match.Value, country);
                }
            }
        }

        return new ZeroVatCheckResult(
            countryConfirmed ? ZeroVatCheckOutcome.ConfirmedNoBasisFound : ZeroVatCheckOutcome.NoBasisFound,
            null, country);
    }
}
