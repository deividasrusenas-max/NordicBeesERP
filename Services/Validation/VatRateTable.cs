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
/// VAT-rate whitelist by supplier country and invoice date (PLAN-ETAPAS1 §2, D-038 Q2). Pure static,
/// no I/O — OCR Etapas 1 prep (S1c), NOT wired in anywhere.
/// <para>
/// UNCONFIRMED: every row comes from <c>Docs/ocr-rebuild/analysis/RESEARCH-2026-09-25-reliability.md</c>
/// §3, which cites secondary sources only. None has been confirmed by the accountant against the EC
/// TEDB (Taxes in Europe Database); <see cref="AllRowsConfirmed"/> is false and <see cref="LastVerified"/>
/// is null until that happens. Per D-038 the rate gate must not be wired (S6) before it does.
/// Dates that research does not give are NOT invented: rows start where the source's statement
/// starts, and unknown values are TODO rows.
/// </para>
/// </summary>
public static class VatRateTable
{
    /// <summary>Date the accountant last confirmed the table against EC TEDB; null = never.</summary>
    public static readonly DateTime? LastVerified = null;

    /// <summary>Date of the research the unconfirmed values come from.</summary>
    public static readonly DateTime LastResearched = new(2026, 9, 25);

    private const string Research = "RESEARCH-2026-09-25-reliability.md §3 (Tax Foundation, 2026 VAT Rates in Europe) — UNCONFIRMED";
    private const string ResearchRo = "RESEARCH-2026-09-25-reliability.md §3 (VATupdate; Fiscal Solutions; GD 602/2025) — UNCONFIRMED";

    public static readonly IReadOnlyList<VatRateRow> Rows = new[]
    {
        // Research gives these as the 2026 rates only; validity before 2026-01-01 was not researched.
        new VatRateRow("LT", new DateTime(2026, 1, 1), null, new[] { 21m, 9m, 5m }, VatRateRowStatus.Unconfirmed, Research),
        new VatRateRow("DE", new DateTime(2026, 1, 1), null, new[] { 19m, 7m }, VatRateRowStatus.Unconfirmed, Research),
        new VatRateRow("LV", new DateTime(2026, 1, 1), null, new[] { 21m, 12m, 5m }, VatRateRowStatus.Unconfirmed, Research),
        // TODO(accountant): EE's change to 24 % — the change date is not in the research; rows before 2026 absent.
        new VatRateRow("EE", new DateTime(2026, 1, 1), null, new[] { 24m, 13m, 9m }, VatRateRowStatus.Unconfirmed, Research),

        // RO: standard 19 % → 21 %, reduced 5 % / 9 % merged into 11 %, from 2025-08-01.
        // The research does not say since when 19 / 9 / 5 applied → ValidFrom null.
        new VatRateRow("RO", null, new DateTime(2025, 7, 31), new[] { 19m, 9m, 5m }, VatRateRowStatus.Unconfirmed, ResearchRo),
        new VatRateRow("RO", new DateTime(2025, 8, 1), null, new[] { 21m, 11m }, VatRateRowStatus.Unconfirmed, ResearchRo),

        // TODO(accountant): PL rates are not in the research (PL is in the table per D-038 Q2).
        new VatRateRow("PL", null, null, Array.Empty<decimal>(), VatRateRowStatus.Todo,
            "not researched — TODO: confirm PL rates against EC TEDB"),
    };

    /// <summary>True only when every row is <see cref="VatRateRowStatus.Confirmed"/>. False today.</summary>
    public static bool AllRowsConfirmed => Rows.All(r => r.Status == VatRateRowStatus.Confirmed);

    /// <summary>
    /// Checks <paramref name="ratePercent"/> (e.g. 21) against the rates of <paramref name="countryCode"/>
    /// (ISO 3166 alpha-2, case-insensitive) on <paramref name="date"/> (time ignored). 0 % is always
    /// <see cref="VatRateCheckOutcome.ZeroRate"/>. Never throws.
    /// </summary>
    public static VatRateCheckResult Check(string? countryCode, DateTime date, decimal ratePercent)
    {
        if (ratePercent == 0m) return new VatRateCheckResult(VatRateCheckOutcome.ZeroRate, null);

        var country = countryCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(country) || !Rows.Any(r => r.Country == country))
            return new VatRateCheckResult(VatRateCheckOutcome.UnknownCountry, null);

        var day = date.Date;
        var row = Rows.FirstOrDefault(r => r.Country == country
            && (r.ValidFrom is null || r.ValidFrom.Value.Date <= day)
            && (r.ValidTo is null || day <= r.ValidTo.Value.Date));
        if (row is null || row.Status == VatRateRowStatus.Todo || row.Rates.Count == 0)
            return new VatRateCheckResult(VatRateCheckOutcome.NoRateData, row);

        return new VatRateCheckResult(row.Rates.Contains(ratePercent) ? VatRateCheckOutcome.Allowed : VatRateCheckOutcome.NotAllowed, row);
    }
}
