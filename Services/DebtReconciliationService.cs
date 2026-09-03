using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;

namespace NordicBeesERP.Services;

public class DebtReconciliationService : IDebtReconciliationService
{
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public DebtReconciliationService(IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<DebtReconciliationResult> GetReconciliationAsync(int partnerId, int year, int? endMonth)
    {
        var periodStart = new DateTime(year, 1, 1);
        var periodEnd = endMonth.HasValue
            ? new DateTime(year, endMonth.Value, DateTime.DaysInMonth(year, endMonth.Value))
            : new DateTime(year, 12, 31);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var partner = await db.BusinessPartners.AsNoTracking()
            .Where(b => b.Id == partnerId)
            .Select(b => new
            {
                b.Id,
                b.Name,
                b.CompanyCode,
                b.VatCode,
                b.Address,
                b.City,
                b.PostalCode,
                b.Country
            })
            .FirstOrDefaultAsync();

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.CustomerId == partnerId
                        && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                        && i.Status != InvoiceStatus.Draft
                        && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new { i.Id, i.InvoiceNumber, i.InvoiceDate, i.TotalInclVat, i.DueDate, i.PaymentDueDate })
            .ToListAsync();

        var creditNotes = await db.CreditNotes.AsNoTracking()
            .Where(c => c.CustomerId == partnerId
                        && c.Status != CreditNoteStatus.Draft
                        && c.Status != CreditNoteStatus.Disputed)
            .Select(c => new { c.Id, c.CreditNoteNumber, c.CreditDate, c.TotalInclVat })
            .ToListAsync();

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.CustomerId == partnerId)
            .Select(p => new { p.Id, p.PaymentDate, p.Amount, p.ReferenceNumber })
            .ToListAsync();

        var entries = new List<(DateTime Date, string Number, DateTime? DueDate, decimal Debit, decimal Credit)>();

        foreach (var inv in invoices)
        {
            entries.Add((inv.InvoiceDate, inv.InvoiceNumber, inv.DueDate ?? inv.PaymentDueDate, inv.TotalInclVat, 0m));
        }
        foreach (var cn in creditNotes)
        {
            entries.Add((cn.CreditDate, cn.CreditNoteNumber, (DateTime?)null, 0m, cn.TotalInclVat));
        }
        foreach (var pay in payments)
        {
            var number = !string.IsNullOrWhiteSpace(pay.ReferenceNumber) ? pay.ReferenceNumber! : $"MOK-{pay.Id}";
            entries.Add((pay.PaymentDate, number, (DateTime?)null, 0m, pay.Amount));
        }

        decimal openingBalance = entries
            .Where(e => e.Date < periodStart)
            .Sum(e => e.Debit - e.Credit);

        var periodEntries = entries
            .Where(e => e.Date >= periodStart && e.Date <= periodEnd)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Number)
            .ToList();

        var lines = new List<DebtReconciliationLine>();
        decimal running = openingBalance;
        foreach (var e in periodEntries)
        {
            running += e.Debit - e.Credit;
            ReconciliationSourceType source = e.Debit > 0
                ? ReconciliationSourceType.Invoice
                : (e.Number.StartsWith("KLAK", StringComparison.OrdinalIgnoreCase)
                    ? ReconciliationSourceType.CreditNote
                    : ReconciliationSourceType.Payment);
            lines.Add(new DebtReconciliationLine
            {
                DocumentDate = e.Date,
                DocumentNumber = e.Number,
                DueDate = e.DueDate,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = running,
                SourceType = source
            });
        }

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        var closingBalance = openingBalance + totalDebit - totalCredit;

        var addressParts = new[] { partner?.Address, partner?.PostalCode, partner?.City, partner?.Country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToList();
        var partnerAddress = string.Join(", ", addressParts);

        return new DebtReconciliationResult
        {
            PartnerName = partner?.Name ?? string.Empty,
            PartnerCode = partner?.CompanyCode ?? string.Empty,
            PartnerAddress = partnerAddress,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            OpeningBalance = openingBalance,
            Lines = lines,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            ClosingBalance = closingBalance
        };
    }

    public async Task<Dictionary<int, decimal>> GetBalancesBulkAsync(IEnumerable<int> partnerIds, int? year = null)
    {
        var partnerIdList = partnerIds.Distinct().ToList();
        if (partnerIdList.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        // Fixed set of grouped aggregate queries — no per-partner loop.
        // Sign convention matches GetReconciliationAsync: debit (invoices) − credit (credit notes + payments).
        // In the year-filtered branch, the opening balance also subtracts pre-period payments.

        Dictionary<int, decimal> debit, creditCn, creditPay;
        Dictionary<int, decimal>? openingDebit = null, openingCredit = null, openingPayments = null;

        if (!year.HasValue)
        {
            var invoiceRows = await db.Invoices.AsNoTracking()
                .Where(i => partnerIdList.Contains(i.CustomerId)
                    && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                    && i.Status != InvoiceStatus.Draft
                    && i.Status != InvoiceStatus.Cancelled)
                .GroupBy(i => i.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var creditNoteRows = await db.CreditNotes.AsNoTracking()
                .Where(c => partnerIdList.Contains(c.CustomerId)
                    && c.Status != CreditNoteStatus.Draft
                    && c.Status != CreditNoteStatus.Disputed)
                .GroupBy(c => c.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var paymentRows = await db.Payments.AsNoTracking()
                .Where(p => partnerIdList.Contains(p.CustomerId))
                .GroupBy(p => p.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            debit = invoiceRows.ToDictionary(r => r.Key, r => r.Total);
            creditCn = creditNoteRows.ToDictionary(r => r.Key, r => r.Total);
            creditPay = paymentRows.ToDictionary(r => r.Key, r => r.Total);
        }
        else
        {
            var y = year.Value;
            var periodStart = new DateTime(y, 1, 1);
            var periodEnd = new DateTime(y, 12, 31);

            // Opening balance: everything dated before Jan 1 of the year.
            var openingInvoiceRows = await db.Invoices.AsNoTracking()
                .Where(i => partnerIdList.Contains(i.CustomerId)
                    && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                    && i.Status != InvoiceStatus.Draft
                    && i.Status != InvoiceStatus.Cancelled
                    && i.InvoiceDate < periodStart)
                .GroupBy(i => i.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var openingCreditNoteRows = await db.CreditNotes.AsNoTracking()
                .Where(c => partnerIdList.Contains(c.CustomerId)
                    && c.Status != CreditNoteStatus.Draft
                    && c.Status != CreditNoteStatus.Disputed
                    && c.CreditDate < periodStart)
                .GroupBy(c => c.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var openingPaymentRows = await db.Payments.AsNoTracking()
                .Where(p => partnerIdList.Contains(p.CustomerId)
                    && p.PaymentDate < periodStart)
                .GroupBy(p => p.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            var invoiceRows = await db.Invoices.AsNoTracking()
                .Where(i => partnerIdList.Contains(i.CustomerId)
                    && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                    && i.Status != InvoiceStatus.Draft
                    && i.Status != InvoiceStatus.Cancelled
                    && i.InvoiceDate >= periodStart && i.InvoiceDate <= periodEnd)
                .GroupBy(i => i.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var creditNoteRows = await db.CreditNotes.AsNoTracking()
                .Where(c => partnerIdList.Contains(c.CustomerId)
                    && c.Status != CreditNoteStatus.Draft
                    && c.Status != CreditNoteStatus.Disputed
                    && c.CreditDate >= periodStart && c.CreditDate <= periodEnd)
                .GroupBy(c => c.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.TotalInclVat) })
                .ToListAsync();

            var paymentRows = await db.Payments.AsNoTracking()
                .Where(p => partnerIdList.Contains(p.CustomerId)
                    && p.PaymentDate >= periodStart && p.PaymentDate <= periodEnd)
                .GroupBy(p => p.CustomerId)
                .Select(g => new { g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            openingDebit = openingInvoiceRows.ToDictionary(r => r.Key, r => r.Total);
            openingCredit = openingCreditNoteRows.ToDictionary(r => r.Key, r => r.Total);
            openingPayments = openingPaymentRows.ToDictionary(r => r.Key, r => r.Total);
            debit = invoiceRows.ToDictionary(r => r.Key, r => r.Total);
            creditCn = creditNoteRows.ToDictionary(r => r.Key, r => r.Total);
            creditPay = paymentRows.ToDictionary(r => r.Key, r => r.Total);
        }

        var result = new Dictionary<int, decimal>(partnerIdList.Count);
        foreach (var id in partnerIdList)
        {
            var opening = (openingDebit?.GetValueOrDefault(id) ?? 0m)
                        - (openingCredit?.GetValueOrDefault(id) ?? 0m)
                        - (openingPayments?.GetValueOrDefault(id) ?? 0m);
            var balance = opening + debit.GetValueOrDefault(id) - creditCn.GetValueOrDefault(id) - creditPay.GetValueOrDefault(id);
            result[id] = balance;
        }

        return result;
    }
}
