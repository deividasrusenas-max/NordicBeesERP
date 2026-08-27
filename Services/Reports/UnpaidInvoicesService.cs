using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services.Reports;

/// <summary>
/// Builds the statement of unpaid invoices (Neapmokėtų sąskaitų suvestinė) for a
/// single partner over a period. Read-only against the database. Per-invoice
/// remaining balance is computed as TotalInclVat - PaidAmount - credited (credit
/// notes linked via OriginalInvoiceId), reusing the same approach as
/// PaymentService.GetCreditedAmountsByInvoiceAsync — the raw payment_status
/// column is NOT trusted as the source of truth.
/// </summary>
public class UnpaidInvoicesService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public UnpaidInvoicesService(IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UnpaidInvoicesResult> GetUnpaidInvoicesAsync(int partnerId, int year, int? month)
    {
        var periodStart = new DateTime(year, 1, 1);
        var periodEnd = month.HasValue
            ? new DateTime(year, month.Value, DateTime.DaysInMonth(year, month.Value))
            : new DateTime(year, 12, 31);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var partner = await db.BusinessPartners.AsNoTracking()
            .Where(b => b.Id == partnerId)
            .Select(b => new { b.Id, b.Name, b.CompanyCode, b.VatCode, b.Address, b.City, b.PostalCode, b.Country })
            .FirstOrDefaultAsync();

        // Only LAK-prefix sales invoices, not Draft/Cancelled, dated inside the period.
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == partnerId
                        && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                        && i.Status != InvoiceStatus.Draft
                        && i.Status != InvoiceStatus.Cancelled
                        && i.InvoiceDate >= periodStart
                        && i.InvoiceDate <= periodEnd)
            .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.PaymentDueDate, i.TotalInclVat, i.PaidAmount })
            .ToListAsync();

        // Credit-note amounts applied to each invoice (excludes disputed notes).
        var invoiceIds = invoices.Select(i => i.Id).ToList();
        var credited = await GetCreditedAmountsByInvoiceAsync(db, invoiceIds);

        var lines = new List<UnpaidInvoiceLine>();
        foreach (var inv in invoices)
        {
            var remaining = inv.TotalInclVat - inv.PaidAmount - (credited.TryGetValue(inv.Id, out var c) ? c : 0m);
            if (remaining == 0m)
                continue;

            lines.Add(new UnpaidInvoiceLine
            {
                InvoiceNumber = inv.InvoiceNumber,
                InvoiceDate = inv.InvoiceDate,
                DueDate = inv.DueDate ?? inv.PaymentDueDate,
                TotalAmount = inv.TotalInclVat,
                RemainingAmount = remaining
            });
        }

        lines.Sort((a, b) => a.InvoiceDate.CompareTo(b.InvoiceDate));

        var addressParts = new[] { partner?.Address, partner?.PostalCode, partner?.City, partner?.Country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToList();
        var partnerAddress = string.Join(", ", addressParts);

        return new UnpaidInvoicesResult
        {
            PartnerName = partner?.Name ?? string.Empty,
            PartnerCode = partner?.CompanyCode ?? string.Empty,
            PartnerAddress = partnerAddress,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Lines = lines,
            TotalAmount = lines.Sum(l => l.TotalAmount),
            TotalRemaining = lines.Sum(l => l.RemainingAmount)
        };
    }

    /// <summary>
    /// Sum of CreditNote.TotalInclVat per OriginalInvoiceId, excluding disputed
    /// notes. Mirrors PaymentService.GetCreditedAmountsByInvoiceAsync.
    /// </summary>
    private static async Task<Dictionary<int, decimal>> GetCreditedAmountsByInvoiceAsync(NordicBeesERPContext db, List<int> invoiceIds)
    {
        if (invoiceIds.Count == 0)
            return new Dictionary<int, decimal>();

        var rows = await db.CreditNotes.AsNoTracking()
            .Where(cn => cn.OriginalInvoiceId != null && invoiceIds.Contains(cn.OriginalInvoiceId.Value) && cn.Status != CreditNoteStatus.Disputed)
            .Select(cn => new { InvoiceId = cn.OriginalInvoiceId!.Value, Amount = cn.TotalInclVat })
            .ToListAsync();

        var map = new Dictionary<int, decimal>();
        foreach (var row in rows)
            map[row.InvoiceId] = (map.TryGetValue(row.InvoiceId, out var existing) ? existing : 0m) + row.Amount;
        return map;
    }
}
