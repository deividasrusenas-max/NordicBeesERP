using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Validation;

namespace NordicBeesERP.Services;

/// <summary>The partners the matcher sees, one row per partner id (the matcher needs unique candidates), and their default expense categories.</summary>
public sealed record SupplierSnapshot(
    IReadOnlyList<SupplierCandidate> Candidates,
    IReadOnlyDictionary<int, int?> DefaultCategoryIds,
    IReadOnlyDictionary<int, string> Names)
{
    public int? DefaultCategoryOf(int? partnerId) =>
        partnerId.HasValue && DefaultCategoryIds.TryGetValue(partnerId.Value, out var categoryId) ? categoryId : null;
}

/// <summary>One candidate shown next to a PENDING_SUPPLIER invoice: the partner and why the matcher offered it (Etapas 2 S3d).</summary>
public sealed record SupplierCandidateView(
    int PartnerId, string Name, string? VatCode, string? CompanyCode, string? CountryCode,
    MatchOutcome Outcome, MatchTier Tier, MatchReason Reason, bool IsEligible);

/// <summary>
/// The I/O side of the supplier cascade (PLAN-ETAPAS2 §7.1 S3): loads the candidate snapshot from business_partners
/// and runs the pure <see cref="SupplierMatcher"/> over it. One place for the upload path (<c>ExpenseOcrService</c>)
/// and the detail dialog's candidate list (<c>ExpenseService</c>), so both see the same result.
/// </summary>
public static class SupplierMatching
{
    /// <summary>
    /// Every partner, customers and inactive ones included — the matcher decides eligibility itself (D-044 Q7: only active
    /// suppliers are assigned, the rest are only suggested). Known IBANs: active supplier_bank_accounts rows plus the legacy <c>bank_account</c> column.
    /// </summary>
    public static async Task<SupplierSnapshot> LoadSnapshotAsync(NordicBeesERPContext context, CancellationToken ct = default)
    {
        var rows = await context.BusinessPartners
            .Select(b => new
            {
                b.Id, b.Name, b.VatCode, b.CompanyCode, b.CountryCode, b.BankAccount,
                b.IsActive, b.IsSupplier, b.IsExpenseSupplier, b.DefaultExpenseCategoryId
            })
            .ToListAsync(ct);

        var known = (await context.SupplierBankAccounts.Where(a => a.IsActive).Select(a => new { a.PartnerId, a.Iban }).ToListAsync(ct))
            .GroupBy(a => a.PartnerId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.Iban).ToList());

        var candidates = rows
            .Select(r =>
            {
                var ibans = known.TryGetValue(r.Id, out var list) ? new List<string>(list) : new List<string>();
                if (!string.IsNullOrWhiteSpace(r.BankAccount)) ibans.Add(r.BankAccount);
                return new SupplierCandidate(r.Id, r.Name, r.VatCode, r.CompanyCode, r.CountryCode, ibans,
                    r.IsActive, r.IsSupplier || r.IsExpenseSupplier);
            })
            .ToList();

        return new SupplierSnapshot(
            candidates,
            rows.ToDictionary(r => r.Id, r => r.DefaultExpenseCategoryId),
            rows.ToDictionary(r => r.Id, r => r.Name ?? ""));
    }

    /// <summary>
    /// The document as the matcher sees it. A malformed VAT code is never used to match (PLAN-ETAPAS1 §1.2, S5a) — the
    /// hint is the address country only. The other identifiers are passed through; the matcher normalises them.
    /// </summary>
    public static SupplierDocument Document(string? name, string? vatCode, string? companyCode, string? iban, string? countryHint)
    {
        var vatFormatWrong = VatCodeFormatValidator.Validate(vatCode, countryHint).Reason == VatCodeValidationReason.WrongFormat;
        return new SupplierDocument(name, vatFormatWrong ? null : vatCode, companyCode, iban, countryHint);
    }

    public static async Task<(SupplierMatch Match, SupplierSnapshot Snapshot)> MatchAsync(
        IDbContextFactory<NordicBeesERPContext> dbFactory, SupplierDocument document, CancellationToken ct = default)
    {
        await using var context = dbFactory.CreateDbContext();
        var snapshot = await LoadSnapshotAsync(context, ct);
        return (SupplierMatcher.Match(document, snapshot.Candidates), snapshot);
    }

    /// <summary>The audit text of a match (no personal data beyond ids): parsed by nothing, read by people and the staging queries.</summary>
    public static string DescribeForAudit(SupplierMatch match) =>
        $"outcome={match.Outcome}; tier={match.Tier}; reason={match.Reason}; candidates={string.Join(",", match.CandidateIds)}";
}
