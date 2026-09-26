namespace NordicBeesERP.Services.Validation;

/// <summary>Confirmation state of one <see cref="VatRateRow"/>.</summary>
public enum VatRateRowStatus
{
    /// <summary>Values taken from research; NOT yet confirmed by the accountant against EC TEDB (D-038 Q2).</summary>
    Unconfirmed,

    /// <summary>Values not known yet — the row exists only to mark the gap. Never used for a verdict.</summary>
    Todo,

    /// <summary>Confirmed by the accountant against EC TEDB.</summary>
    Confirmed
}

/// <summary>
/// Legal VAT rates (percent, 0 excluded) of one country for a date range. <see cref="ValidFrom"/>
/// null = the source does not say since when; <see cref="ValidTo"/> null = still in force.
/// Both bounds are inclusive.
/// </summary>
public sealed record VatRateRow(
    string Country, DateTime? ValidFrom, DateTime? ValidTo, IReadOnlyList<decimal> Rates,
    VatRateRowStatus Status, string Source);

/// <summary>Outcome of <see cref="VatRateTable.Check"/>.</summary>
public enum VatRateCheckOutcome
{
    /// <summary>The rate is one of the country's rates on that date.</summary>
    Allowed,

    /// <summary>The country and date are covered, and the rate is not one of its rates.</summary>
    NotAllowed,

    /// <summary>0 % — never judged here; that is ZERO_VAT's job (D-026).</summary>
    ZeroRate,

    /// <summary>The country is not in the table (or no country was given).</summary>
    UnknownCountry,

    /// <summary>The country is in the table, but no usable row covers the date (outside the researched range, or a TODO row).</summary>
    NoRateData
}

/// <summary>Result of <see cref="VatRateTable.Check"/>; <see cref="Row"/> is the row used, when one was.</summary>
public sealed record VatRateCheckResult(VatRateCheckOutcome Outcome, VatRateRow? Row);

/// <summary>
/// VAT-rate whitelist by supplier country and invoice date (PLAN-ETAPAS1 §2, D-038 Q2, D-039 item 3). Pure
/// static, no I/O. Wired into the expense gates in S6 (<c>ExpenseService.RecomputeVatRateFlags</c>).
/// <para>
/// <b>Every row is UNCONFIRMED.</b> Values come from <c>Docs/ocr-rebuild/analysis/RESEARCH-2026-09-25-reliability.md</c>
/// §3 (secondary sources) or, where the row's source comment says "general knowledge", from the author's general
/// knowledge (CZ 21/12, ES 21/10/4, PL 23/8/5) — none is confirmed by the accountant against the EC TEDB (Taxes in
/// Europe Database). Validity starts 2025-01-01 for every country (D-039 item 3): an earlier invoice date is
/// "no rate data". Dates the sources do not give are NOT invented — e.g. the EE change 22 → 24 %.
/// </para>
/// <para>
/// <b>How a row becomes CONFIRMED (data-only change, this file only).</b> The single place is the
/// <see cref="VatRateRowStatus"/> argument of that row: change <c>VatRateRowStatus.Unconfirmed</c> to
/// <c>VatRateRowStatus.Confirmed</c> (and update the row's source comment with what the accountant checked), then
/// set <see cref="LastVerified"/> to the confirmation date and update the tests that pin "nothing confirmed yet".
/// Behaviour: on an UNCONFIRMED row the gate only says VAT_RATE_UNCHECKED (information); on a CONFIRMED row a rate
/// that is not in the row's list becomes VAT_RATE_NOT_ALLOWED (review). Do not confirm EE before its 22 → 24 %
/// change date is known and the row is split at that date.
/// </para>
/// </summary>
public static class VatRateTable
{
    /// <summary>Date the accountant last confirmed the table against EC TEDB; null = never.</summary>
    public static readonly DateTime? LastVerified = null;

    /// <summary>Date of the research the unconfirmed values come from.</summary>
    public static readonly DateTime LastResearched = new(2026, 9, 25);

    private static readonly DateTime From2025 = new(2025, 1, 1);

    private const string Research = "RESEARCH-2026-09-25-reliability.md §3 (Tax Foundation, 2026 VAT Rates in Europe) — UNCONFIRMED";
    private const string ResearchRo = "RESEARCH-2026-09-25-reliability.md §3 (VATupdate; Fiscal Solutions; GD 602/2025) — UNCONFIRMED";
    private const string GeneralKnowledge = "author's general knowledge, NOT in the research (D-039 item 3) — UNCONFIRMED, check against EC TEDB";

    public static readonly IReadOnlyList<VatRateRow> Rows = new[]
    {
        // Research gives these as the 2026 rates; validity from 2025-01-01 is the owner's decision (D-039 item 3).
        new VatRateRow("LT", From2025, null, new[] { 21m, 9m, 5m }, VatRateRowStatus.Unconfirmed, Research),
        new VatRateRow("DE", From2025, null, new[] { 19m, 7m }, VatRateRowStatus.Unconfirmed, Research),
        new VatRateRow("LV", From2025, null, new[] { 21m, 12m, 5m }, VatRateRowStatus.Unconfirmed, Research),
        // TODO(accountant): EE's change 22 → 24 % — the change date is not confirmed and is NOT invented here. The row
        // lists only the 2026 rates, so a 22 % invoice of 2025 reads as "not allowed" (unconfirmed → information only).
        // Split the row at the confirmed date BEFORE marking EE confirmed.
        new VatRateRow("EE", From2025, null, new[] { 24m, 13m, 9m }, VatRateRowStatus.Unconfirmed, Research),

        // RO: standard 19 % → 21 %, reduced 5 % / 9 % merged into 11 %, from 2025-08-01 (research).
        // 19 / 9 / 5 is taken to apply from 2025-01-01 by D-039 item 3.
        new VatRateRow("RO", From2025, new DateTime(2025, 7, 31), new[] { 19m, 9m, 5m }, VatRateRowStatus.Unconfirmed, ResearchRo),
        new VatRateRow("RO", new DateTime(2025, 8, 1), null, new[] { 21m, 11m }, VatRateRowStatus.Unconfirmed, ResearchRo),

        // Rates below are from general knowledge, not the research (see GeneralKnowledge).
        new VatRateRow("PL", From2025, null, new[] { 23m, 8m, 5m }, VatRateRowStatus.Unconfirmed, "PL 23 / 8 / 5 — " + GeneralKnowledge),
        new VatRateRow("CZ", From2025, null, new[] { 21m, 12m }, VatRateRowStatus.Unconfirmed, "CZ 21 / 12 — " + GeneralKnowledge),
        new VatRateRow("ES", From2025, null, new[] { 21m, 10m, 4m }, VatRateRowStatus.Unconfirmed, "ES 21 / 10 / 4 — " + GeneralKnowledge),
    };

    /// <summary>True only when every row is <see cref="VatRateRowStatus.Confirmed"/>. False today.</summary>
    public static bool AllRowsConfirmed => Rows.All(r => r.Status == VatRateRowStatus.Confirmed);

    /// <summary>
    /// Checks <paramref name="ratePercent"/> (e.g. 21) against the rates of <paramref name="countryCode"/>
    /// (ISO 3166 alpha-2, case-insensitive) on <paramref name="date"/> (time ignored). 0 % is always
    /// <see cref="VatRateCheckOutcome.ZeroRate"/>. Never throws.
    /// </summary>
    public static VatRateCheckResult Check(string? countryCode, DateTime date, decimal ratePercent) =>
        Check(countryCode, date, ratePercent, Rows);

    /// <summary><see cref="Check(string?, DateTime, decimal)"/> against a given row set (tests use it to exercise CONFIRMED rows).</summary>
    public static VatRateCheckResult Check(string? countryCode, DateTime date, decimal ratePercent, IReadOnlyList<VatRateRow> rows)
    {
        if (ratePercent == 0m) return new VatRateCheckResult(VatRateCheckOutcome.ZeroRate, null);

        var country = countryCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(country) || !rows.Any(r => r.Country == country))
            return new VatRateCheckResult(VatRateCheckOutcome.UnknownCountry, null);

        var day = date.Date;
        var row = rows.FirstOrDefault(r => r.Country == country
            && (r.ValidFrom is null || r.ValidFrom.Value.Date <= day)
            && (r.ValidTo is null || day <= r.ValidTo.Value.Date));
        if (row is null || row.Status == VatRateRowStatus.Todo || row.Rates.Count == 0)
            return new VatRateCheckResult(VatRateCheckOutcome.NoRateData, row);

        return new VatRateCheckResult(row.Rates.Contains(ratePercent) ? VatRateCheckOutcome.Allowed : VatRateCheckOutcome.NotAllowed, row);
    }
}
