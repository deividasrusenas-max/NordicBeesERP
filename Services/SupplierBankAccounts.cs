using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Validation;

namespace NordicBeesERP.Services;

/// <summary>
/// A partner's known IBANs (PLAN-ETAPAS2 §1.4; D-044 Q2, Q5). One place for reading them (the matcher snapshot, the
/// SUPPLIER_NEW_IBAN rule) and writing them (partner save, the „Pridėti IBAN prie tiekėjo" action). Only IBANs that pass
/// <see cref="IbanValidator"/> are ever stored or compared — an invalid IBAN is never a key and never added.
/// </summary>
public static class SupplierBankAccounts
{
    /// <summary>
    /// The valid, normalised IBANs known for the partner: active rows of supplier_bank_accounts plus the legacy
    /// <c>business_partners.bank_account</c> when it is a valid IBAN.
    /// </summary>
    public static async Task<HashSet<string>> KnownAsync(NordicBeesERPContext context, int partnerId)
    {
        var rows = await context.SupplierBankAccounts
            .Where(a => a.PartnerId == partnerId && a.IsActive)
            .Select(a => a.Iban)
            .ToListAsync();
        var legacy = await context.BusinessPartners
            .Where(b => b.Id == partnerId)
            .Select(b => b.BankAccount)
            .FirstOrDefaultAsync();

        return rows.Append(legacy)
            .Select(SupplierIdentityNormalizer.Iban)
            .Where(i => i != null)
            .Select(i => i!)
            .ToHashSet();
    }

    /// <summary>
    /// Inserts the IBAN for the partner unless the pair already exists (parameterised, race-safe through
    /// <c>uq_partner_iban</c>). Returns false for an invalid IBAN or an existing pair. The stored value is the normalised IBAN.
    /// </summary>
    public static async Task<bool> EnsureKnownAsync(NordicBeesERPContext context, int partnerId, string? rawIban,
        string source, int? sourceInvoiceId, string? createdBy)
    {
        var iban = SupplierIdentityNormalizer.Iban(rawIban);
        if (iban == null) return false;

        var inserted = await context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO supplier_bank_accounts (partner_id, iban, source, source_invoice_id, is_active, created_at, created_by)
            SELECT {0}, {1}, {2}, {3}, 1, {4}, {5} FROM DUAL
            WHERE NOT EXISTS (SELECT 1 FROM supplier_bank_accounts WHERE partner_id = {0} AND iban = {1})",
            partnerId, iban, source, sourceInvoiceId, DateTime.UtcNow, createdBy);
        return inserted > 0;
    }

    /// <summary>First four and last four characters — enough for a human to recognise the account, not the whole IBAN, in audit rows.</summary>
    public static string Mask(string iban) =>
        iban.Length <= 8 ? iban : $"{iban[..4]}…{iban[^4..]}";
}
