using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Validation;

namespace NordicBeesERP.Services;

/// <summary>
/// Alias learning (PLAN-ETAPAS2 §2, D-017, D-044 Q4). An alias is "this normalised OCR supplier NAME means that partner" — never a VAT
/// code, company code or IBAN. It is created only by an explicit human action (assigning or changing a supplier on one invoice,
/// creating a supplier for one invoice) — never by the create-supplier sweep and never by a matcher assignment. CANDIDATE becomes
/// ACTIVE after <see cref="PromotionThreshold"/> confirmations from DISTINCT invoices; a confirmation of the same key for a different
/// partner freezes both; a FROZEN or REVOKED alias is never applied. No retroactive sweep: promotion reassigns nothing. All writes are
/// parameterised; state changes and their event rows share one transaction.
/// </summary>
public static class SupplierAliases
{
    public const int PromotionThreshold = 2;

    public const string Candidate = "CANDIDATE";
    public const string Active = "ACTIVE";
    public const string Frozen = "FROZEN";
    public const string Revoked = "REVOKED";

    /// <summary>The alias key of an OCR supplier name, or null when nothing is left after normalisation (such a key must never match).</summary>
    public static string? KeyFor(string? rawName)
    {
        var key = SupplierIdentityNormalizer.NameNormalized(rawName);
        return key.Length == 0 || key.Length > 255 ? null : key;
    }

    /// <summary>The ACTIVE aliases the matcher may apply. Candidates, frozen and revoked ones are never applied.</summary>
    public static async Task<List<ActiveAlias>> LoadActiveAsync(NordicBeesERPContext context, CancellationToken ct = default) =>
        (await context.SupplierAliases.Where(a => a.State == Active).Select(a => new { a.AliasKey, a.PartnerId }).ToListAsync(ct))
            .Select(a => new ActiveAlias(a.AliasKey, a.PartnerId)).ToList();

    public static Task<List<SupplierAlias>> ListAsync(NordicBeesERPContext context, int partnerId) =>
        context.SupplierAliases.Where(a => a.PartnerId == partnerId).OrderBy(a => a.State).ThenBy(a => a.AliasKey).ToListAsync();

    /// <summary>
    /// Records the explicit confirmation "invoice <paramref name="invoiceId"/>, whose OCR supplier name was <paramref name="rawName"/>, belongs
    /// to <paramref name="partnerId"/>". Counts once per invoice (a re-OCR or a repeated click does not count twice); promotes at N; freezes
    /// both aliases on a conflict. An alias of the partner's own name adds nothing and is not created.
    /// </summary>
    public static async Task ConfirmAsync(NordicBeesERPContext context, int invoiceId, int partnerId, string? rawName, string actor)
    {
        var key = KeyFor(rawName);
        if (key == null) return;
        var partnerName = await context.BusinessPartners.Where(b => b.Id == partnerId).Select(b => b.Name).FirstOrDefaultAsync();
        if (partnerName == null || SupplierIdentityNormalizer.NameNormalized(partnerName) == key) return;

        await InTransactionAsync(context, async () =>
        {
            var now = DateTime.Now;
            var rows = await context.SupplierAliases.Where(a => a.AliasKey == key).ToListAsync();
            var mine = rows.FirstOrDefault(r => r.PartnerId == partnerId);

            if (mine == null)
            {
                await context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO supplier_aliases (partner_id, alias_key, raw_example, state, confirmations, frozen_reason, created_at, updated_at)
                    VALUES ({0}, {1}, {2}, {3}, 0, NULL, {4}, {4})",
                    partnerId, key, Truncate(rawName!.Trim(), 255), Candidate, now);
                mine = await context.SupplierAliases.FirstAsync(a => a.AliasKey == key && a.PartnerId == partnerId);
                rows.Add(mine);
            }
            else if (mine.State == Revoked)
            {
                // a human revoked it and a human now confirms it again: start over
                await SetStateAsync(context, mine.Id, Candidate, 0, null, now);
                mine.State = Candidate;
                mine.Confirmations = 0;
            }

            // one confirmation per invoice, counted after the last revocation
            var lastRevocation = await context.SupplierAliasEvents
                .Where(e => e.AliasId == mine.Id && e.Event == "REVOKED").Select(e => (int?)e.Id).MaxAsync() ?? 0;
            var counted = await context.SupplierAliasEvents
                .AnyAsync(e => e.AliasId == mine.Id && e.Event == "CONFIRMED" && e.InvoiceId == invoiceId && e.Id > lastRevocation);
            if (!counted)
            {
                await AddEventAsync(context, mine.Id, invoiceId, "CONFIRMED", actor, $"partner_id={partnerId}", now);
                mine.Confirmations += 1;
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE supplier_aliases SET confirmations = {0}, updated_at = {1} WHERE id = {2}", mine.Confirmations, now, mine.Id);
            }

            // the same key confirmed for a different partner: both frozen, nothing is applied until a human decides
            var others = rows.Where(r => r.PartnerId != partnerId && r.State != Revoked).ToList();
            if (others.Count > 0)
            {
                var reason = $"Konfliktas: tas pats pavadinimas patvirtintas tiekėjams {string.Join(", ", others.Select(o => o.PartnerId).Append(partnerId).OrderBy(x => x))}";
                foreach (var row in others.Append(mine).Where(r => r.State != Frozen))
                {
                    await SetStateAsync(context, row.Id, Frozen, row.Confirmations, Truncate(reason, 255), now);
                    await AddEventAsync(context, row.Id, invoiceId, "CONFLICT_FROZEN", actor, reason, now);
                    row.State = Frozen;
                }
                return;
            }

            if (mine.State == Candidate && mine.Confirmations >= PromotionThreshold)
            {
                await SetStateAsync(context, mine.Id, Active, mine.Confirmations, null, now);
                await AddEventAsync(context, mine.Id, invoiceId, "PROMOTED", actor, $"{mine.Confirmations} patvirtinimai", now);
            }
        });
    }

    /// <summary>Human revoke: the alias is never applied again until a human confirms it afresh.</summary>
    public static async Task RevokeAsync(NordicBeesERPContext context, int aliasId, string actor)
    {
        var alias = await context.SupplierAliases.FirstOrDefaultAsync(a => a.Id == aliasId)
            ?? throw new InvalidOperationException("Aliasas nerastas");
        if (alias.State == Revoked) throw new InvalidOperationException("Aliasas jau atšauktas");

        await InTransactionAsync(context, async () =>
        {
            var now = DateTime.Now;
            await SetStateAsync(context, aliasId, Revoked, alias.Confirmations, null, now);
            await AddEventAsync(context, aliasId, null, "REVOKED", actor, $"buvo {alias.State}", now);
        });
    }

    /// <summary>
    /// Human unfreeze: back to ACTIVE when it already has N confirmations, else CANDIDATE. Refused while another partner holds an ACTIVE
    /// alias of the same key — that one must be revoked first, so a key never has two active partners.
    /// </summary>
    public static async Task UnfreezeAsync(NordicBeesERPContext context, int aliasId, string actor)
    {
        var alias = await context.SupplierAliases.FirstOrDefaultAsync(a => a.Id == aliasId)
            ?? throw new InvalidOperationException("Aliasas nerastas");
        if (alias.State != Frozen) throw new InvalidOperationException("Atblokuoti galima tik užblokuotą aliasą");
        if (await context.SupplierAliases.AnyAsync(a => a.AliasKey == alias.AliasKey && a.PartnerId != alias.PartnerId && a.State == Active))
            throw new InvalidOperationException("Kitas tiekėjas jau turi aktyvų šio pavadinimo aliasą — pirmiausia jį atšaukite");

        await InTransactionAsync(context, async () =>
        {
            var now = DateTime.Now;
            var state = alias.Confirmations >= PromotionThreshold ? Active : Candidate;
            await SetStateAsync(context, aliasId, state, alias.Confirmations, null, now);
            await AddEventAsync(context, aliasId, null, "UNFROZEN", actor, $"-> {state}", now);
        });
    }

    /// <summary>The matcher applied this alias to an invoice (create / re-OCR): recorded so the alias list shows where it worked.</summary>
    public static async Task RecordAppliedAsync(NordicBeesERPContext context, int invoiceId, string? rawName, int partnerId, string actor)
    {
        var key = KeyFor(rawName);
        if (key == null) return;
        var aliasId = await context.SupplierAliases
            .Where(a => a.AliasKey == key && a.PartnerId == partnerId && a.State == Active).Select(a => (int?)a.Id).FirstOrDefaultAsync();
        if (aliasId == null) return;
        await AddEventAsync(context, aliasId.Value, invoiceId, "APPLIED", actor, null, DateTime.Now);
    }

    public static async Task<List<SupplierAliasEvent>> EventsAsync(NordicBeesERPContext context, int aliasId) =>
        await context.SupplierAliasEvents.Where(e => e.AliasId == aliasId).OrderBy(e => e.Id).ToListAsync();

    // ------------------------------------------------------------------ helpers

    private static async Task InTransactionAsync(NordicBeesERPContext context, Func<Task> work)
    {
        if (context.Database.CurrentTransaction != null)
        {
            await work();
            return;
        }
        await using var transaction = await context.Database.BeginTransactionAsync();
        await work();
        await transaction.CommitAsync();
    }

    private static Task<int> SetStateAsync(NordicBeesERPContext context, int aliasId, string state, int confirmations, string? frozenReason, DateTime now) =>
        context.Database.ExecuteSqlRawAsync(
            "UPDATE supplier_aliases SET state = {0}, confirmations = {1}, frozen_reason = {2}, updated_at = {3} WHERE id = {4}",
            state, confirmations, frozenReason, now, aliasId);

    private static Task<int> AddEventAsync(NordicBeesERPContext context, int aliasId, int? invoiceId, string kind, string? actor, string? details, DateTime now) =>
        context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO supplier_alias_events (alias_id, invoice_id, event, actor, details, created_at)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})",
            aliasId, invoiceId, kind, actor, details, now);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
