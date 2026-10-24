using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;

namespace NordicBeesERP.Services;

public class OffsetDocument
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Total { get; set; }
    public decimal Open { get; set; }
}

public class OffsetLineRequest
{
    public int DocumentId { get; set; }
    public decimal Amount { get; set; }
}

public class CreateOffsetRequest
{
    public int PartnerId { get; set; }
    public DateTime OffsetDate { get; set; }
    public List<OffsetLineRequest> Receivables { get; set; } = new();
    public List<OffsetLineRequest> Payables { get; set; } = new();
    public string? Notes { get; set; }
}

public class SettlementListItem
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public int PartnerId { get; set; }
    public string PartnerName { get; set; } = "";
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
}

public class SettlementDetail
{
    public SettlementOffset Offset { get; set; } = new();
    public string PartnerName { get; set; } = "";
    public List<(SettlementOffsetLine Line, string DocNumber, DateTime DocDate)> Lines { get; set; } = new();
}

public interface ISettlementOffsetService
{
    Task<List<(int Id, string Name)>> GetCandidatePartnersAsync();
    Task<List<OffsetDocument>> GetOpenReceivablesAsync(int partnerId);
    Task<List<OffsetDocument>> GetOpenPayablesAsync(int partnerId);
    Task<List<SettlementListItem>> ListAsync();
    Task<SettlementDetail?> GetAsync(int id);
    Task<int> CreateAndConfirmAsync(CreateOffsetRequest request, int userId);
    Task CancelAsync(int id, string reason, int userId);
}

/// <summary>
/// Užskaita phase 1: LAK sales invoices vs expense invoices of the SAME business_partners.id. Confirm and cancel each run in
/// one transaction and write ordinary payments / expense_payments rows with source = 'offset', so every existing balance reader
/// (recalc, lists, aging, dunning, statements) stays correct without changes.
/// </summary>
public class SettlementOffsetService : ISettlementOffsetService
{
    public static readonly DateTime EarliestDocumentDate = new(2026, 1, 1);
    public const string Source = "offset";

    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public SettlementOffsetService(IDbContextFactory<NordicBeesERPContext> dbFactory) => _dbFactory = dbFactory;

    // ───────────────────────── open documents ─────────────────────────

    public async Task<List<OffsetDocument>> GetOpenReceivablesAsync(int partnerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await LoadOpenReceivablesAsync(db, partnerId, null);
    }

    public async Task<List<OffsetDocument>> GetOpenPayablesAsync(int partnerId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await LoadOpenPayablesAsync(db, partnerId, null);
    }

    private static async Task<List<OffsetDocument>> LoadOpenReceivablesAsync(NordicBeesERPContext db, int partnerId, List<int>? onlyIds)
    {
        var query = db.Invoices.AsNoTracking().Where(i => i.CustomerId == partnerId
            && EF.Functions.Like(i.InvoiceNumber, "LAK%") && !EF.Functions.Like(i.InvoiceNumber, "ULAK%")
            && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Disputed
            && i.InvoiceDate >= EarliestDocumentDate);
        if (onlyIds != null) query = query.Where(i => onlyIds.Contains(i.Id));
        var invoices = await query.ToListAsync();
        if (invoices.Count == 0) return new();

        var ids = invoices.Select(i => i.Id).ToList();
        // EUR only: a null currency means the default (EUR); anything else must be the EUR currency row.
        var eurId = await db.Currencies.AsNoTracking().Where(c => c.Code == "EUR").Select(c => (int?)c.Id).FirstOrDefaultAsync();
        invoices = invoices.Where(i => i.CurrencyId == null || i.CurrencyId == eurId).ToList();

        var allocated = await db.PaymentAllocations.AsNoTracking().Where(a => ids.Contains(a.InvoiceId))
            .GroupBy(a => a.InvoiceId).Select(g => new { g.Key, Sum = g.Sum(a => a.AllocatedAmount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);
        var credited = await CreditNoteDebtRule.GetCreditedByInvoiceAsync(db, ids);

        return invoices
            .Select(i => new OffsetDocument
            {
                Id = i.Id, Number = i.InvoiceNumber, Date = i.InvoiceDate, DueDate = InvoiceBalanceRule.DueDate(i.DueDate, i.PaymentDueDate),
                Total = i.TotalInclVat,
                Open = InvoiceBalanceRule.Remaining(i.TotalInclVat, allocated.GetValueOrDefault(i.Id), credited.GetValueOrDefault(i.Id))
            })
            .Where(d => d.Open > InvoiceBalanceRule.OpenThreshold)
            .OrderBy(d => d.DueDate ?? d.Date).ThenBy(d => d.Number)
            .ToList();
    }

    private static async Task<List<OffsetDocument>> LoadOpenPayablesAsync(NordicBeesERPContext db, int partnerId, List<int>? onlyIds)
    {
        var query = db.ExpenseInvoices.AsNoTracking().WhereCountsAsPayable().Where(i => i.SupplierId == partnerId
            && i.Status != "PENDING_SUPPLIER" && i.Status != "NEEDS_REVIEW"
            && i.Currency == "EUR" && i.InvoiceDate >= EarliestDocumentDate);
        if (onlyIds != null) query = query.Where(i => onlyIds.Contains(i.Id));
        var invoices = await query.ToListAsync();
        if (invoices.Count == 0) return new();

        var ids = invoices.Select(i => i.Id).ToList();
        var paid = await db.ExpensePayments.AsNoTracking().Where(p => ids.Contains(p.InvoiceId))
            .GroupBy(p => p.InvoiceId).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToDictionaryAsync(x => x.Key, x => x.Sum);

        return invoices
            .Select(i => new OffsetDocument
            {
                Id = i.Id, Number = i.InvoiceNumber, Date = i.InvoiceDate, DueDate = i.DueDate, Total = i.AmountInclVat,
                Open = i.AmountInclVat - paid.GetValueOrDefault(i.Id)
            })
            .Where(d => d.Open > InvoiceBalanceRule.OpenThreshold)
            .OrderBy(d => d.DueDate ?? d.Date).ThenBy(d => d.Number)
            .ToList();
    }

    public async Task<List<(int Id, string Name)>> GetCandidatePartnersAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var receivableIds = await db.Invoices.AsNoTracking()
            .Where(i => EF.Functions.Like(i.InvoiceNumber, "LAK%") && !EF.Functions.Like(i.InvoiceNumber, "ULAK%")
                && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Disputed
                && i.InvoiceDate >= EarliestDocumentDate)
            .Select(i => i.CustomerId).Distinct().ToListAsync();
        var payableIds = await db.ExpenseInvoices.AsNoTracking().WhereCountsAsPayable()
            .Where(i => i.SupplierId != null && i.InvoiceDate >= EarliestDocumentDate).Select(i => i.SupplierId!.Value).Distinct().ToListAsync();
        var both = receivableIds.Intersect(payableIds).ToList();

        var result = new List<(int, string)>();
        foreach (var partner in await db.BusinessPartners.AsNoTracking().Where(p => both.Contains(p.Id)).OrderBy(p => p.Name).ToListAsync())
        {
            // Keep only partners that really have an open balance on both sides.
            if ((await LoadOpenReceivablesAsync(db, partner.Id, null)).Count > 0 && (await LoadOpenPayablesAsync(db, partner.Id, null)).Count > 0)
                result.Add((partner.Id, partner.Name));
        }
        return result;
    }

    // ───────────────────────── list / detail ─────────────────────────

    public async Task<List<SettlementListItem>> ListAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await (from o in db.SettlementOffsets.AsNoTracking()
                      join p in db.BusinessPartners.AsNoTracking() on o.PartnerId equals p.Id
                      orderby o.OffsetDate descending, o.Id descending
                      select new SettlementListItem
                      {
                          Id = o.Id, Number = o.OffsetNumber, Date = o.OffsetDate, PartnerId = o.PartnerId, PartnerName = p.Name,
                          Amount = o.Amount, Status = o.Status
                      }).ToListAsync();
    }

    public async Task<SettlementDetail?> GetAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var offset = await db.SettlementOffsets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (offset == null) return null;
        var partner = await db.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(p => p.Id == offset.PartnerId);
        var lines = await db.SettlementOffsetLines.AsNoTracking().Where(l => l.OffsetId == id).OrderBy(l => l.Side).ThenBy(l => l.Id).ToListAsync();
        offset.Lines = lines;

        var invoiceIds = lines.Where(l => l.InvoiceId != null).Select(l => l.InvoiceId!.Value).ToList();
        var expenseIds = lines.Where(l => l.ExpenseInvoiceId != null).Select(l => l.ExpenseInvoiceId!.Value).ToList();
        var invoices = await db.Invoices.AsNoTracking().Where(i => invoiceIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
        var expenses = await db.ExpenseInvoices.AsNoTracking().Where(i => expenseIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);

        var detail = new SettlementDetail { Offset = offset, PartnerName = partner?.Name ?? "" };
        foreach (var l in lines)
        {
            if (l.InvoiceId != null && invoices.TryGetValue(l.InvoiceId.Value, out var inv))
                detail.Lines.Add((l, inv.InvoiceNumber, inv.InvoiceDate));
            else if (l.ExpenseInvoiceId != null && expenses.TryGetValue(l.ExpenseInvoiceId.Value, out var exp))
                detail.Lines.Add((l, exp.InvoiceNumber, exp.InvoiceDate));
            else
                detail.Lines.Add((l, "?", offset.OffsetDate));
        }
        return detail;
    }

    // ───────────────────────── confirm ─────────────────────────

    public async Task<int> CreateAndConfirmAsync(CreateOffsetRequest request, int userId)
    {
        ValidateRequestShape(request);

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            // Lock every involved document first (before any other read) so concurrent payments / acts serialize.
            var invoiceIds = request.Receivables.Select(r => r.DocumentId).OrderBy(x => x).ToList();
            var expenseIds = request.Payables.Select(r => r.DocumentId).OrderBy(x => x).ToList();
            await RawSql.QueryAsync(db, "SELECT id FROM invoices WHERE id IN (" + Placeholders(invoiceIds.Count) + ") ORDER BY id FOR UPDATE",
                r => r.Int("id"), default, invoiceIds.Cast<object?>().ToArray());
            await RawSql.QueryAsync(db, "SELECT id FROM expense_invoices WHERE id IN (" + Placeholders(expenseIds.Count) + ") ORDER BY id FOR UPDATE",
                r => r.Int("id"), default, expenseIds.Cast<object?>().ToArray());

            var openReceivables = (await LoadOpenReceivablesAsync(db, request.PartnerId, invoiceIds)).ToDictionary(d => d.Id);
            var openPayables = (await LoadOpenPayablesAsync(db, request.PartnerId, expenseIds)).ToDictionary(d => d.Id);

            foreach (var line in request.Receivables)
            {
                if (!openReceivables.TryGetValue(line.DocumentId, out var doc))
                    throw new InvalidOperationException("Pardavimo sąskaita nepriklauso šiam partneriui, nėra atvira arba netinkama užskaitai.");
                if (line.Amount > doc.Open)
                    throw new InvalidOperationException($"Suma {line.Amount:N2} € viršija sąskaitos {doc.Number} likutį {doc.Open:N2} €.");
            }
            foreach (var line in request.Payables)
            {
                if (!openPayables.TryGetValue(line.DocumentId, out var doc))
                    throw new InvalidOperationException("Pirkimo sąskaita nepriklauso šiam partneriui, nėra atvira arba netinkama užskaitai.");
                if (line.Amount > doc.Open)
                    throw new InvalidOperationException($"Suma {line.Amount:N2} € viršija sąskaitos {doc.Number} likutį {doc.Open:N2} €.");
            }

            var maxDocDate = openReceivables.Values.Select(d => d.Date).Concat(openPayables.Values.Select(d => d.Date)).Max();
            if (request.OffsetDate.Date < maxDocDate.Date)
                throw new InvalidOperationException($"Užskaitos data negali būti ankstesnė už dokumentų datą ({maxDocDate:yyyy-MM-dd}).");

            var number = await NextNumberAsync(db, request.OffsetDate);
            var total = request.Receivables.Sum(r => r.Amount);
            var now = DateTime.UtcNow;

            var offset = new SettlementOffset
            {
                OffsetNumber = number, OffsetDate = request.OffsetDate.Date, PartnerId = request.PartnerId, Currency = "EUR", Amount = total,
                Status = SettlementOffset.StatusConfirmed, Notes = request.Notes, CreatedBy = userId, CreatedAt = now,
                ConfirmedBy = userId, ConfirmedAt = now
            };
            db.SettlementOffsets.Add(offset);
            await db.SaveChangesAsync();

            var payableNumbers = string.Join(", ", openPayables.Values.Select(d => d.Number));
            var receivableNumbers = string.Join(", ", openReceivables.Values.Select(d => d.Number));

            foreach (var line in request.Receivables)
            {
                var doc = openReceivables[line.DocumentId];
                var payment = new Payment
                {
                    PaymentDate = request.OffsetDate.Date, InvoiceId = doc.Id, CustomerId = request.PartnerId, Amount = line.Amount,
                    PaymentMethod = PaymentMethod.Offset, Source = Source, BankConfirmed = true, ReferenceNumber = number,
                    Notes = $"Užskaita su {payableNumbers}", CreatedBy = userId, CreatedAt = now, UpdatedAt = now
                };
                db.Payments.Add(payment);
                await db.SaveChangesAsync();
                db.PaymentAllocations.Add(new PaymentAllocation { PaymentId = payment.Id, InvoiceId = doc.Id, AllocatedAmount = line.Amount, AllocatedAt = now });
                db.PaymentAuditLogs.Add(new PaymentAuditLog
                {
                    PaymentId = payment.Id, InvoiceId = doc.Id, Action = "offset_create", NewAmount = line.Amount, ChangedBy = userId,
                    ChangedAt = now, Notes = $"Užskaita {number}"
                });
                await db.SaveChangesAsync();
                await PaymentService.RecalculateInvoiceStatusInternalAsync(db, doc.Id);
                db.SettlementOffsetLines.Add(new SettlementOffsetLine
                {
                    OffsetId = offset.Id, Side = SettlementOffsetLine.SideReceivable, InvoiceId = doc.Id, Amount = line.Amount,
                    OpenBefore = doc.Open, PaymentId = payment.Id
                });
                await db.SaveChangesAsync();
            }

            foreach (var line in request.Payables)
            {
                var doc = openPayables[line.DocumentId];
                var payment = new ExpensePayment
                {
                    InvoiceId = doc.Id, PaymentDate = request.OffsetDate.Date, Amount = line.Amount, PaymentMethod = "OFFSET",
                    Reference = number, Notes = $"Užskaita su {receivableNumbers}", CreatedAt = now, Source = Source, BankConfirmed = true
                };
                db.ExpensePayments.Add(payment);
                await db.SaveChangesAsync();
                await RecalculateExpenseAsync(db, doc.Id);
                db.SettlementOffsetLines.Add(new SettlementOffsetLine
                {
                    OffsetId = offset.Id, Side = SettlementOffsetLine.SidePayable, ExpenseInvoiceId = doc.Id, Amount = line.Amount,
                    OpenBefore = doc.Open, ExpensePaymentId = payment.Id
                });
                await db.SaveChangesAsync();
            }

            await tx.CommitAsync();
            return offset.Id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static void ValidateRequestShape(CreateOffsetRequest r)
    {
        if (r.PartnerId <= 0) throw new InvalidOperationException("Pasirinkite partnerį.");
        if (r.Receivables.Count == 0 || r.Payables.Count == 0)
            throw new InvalidOperationException("Pasirinkite bent po vieną pardavimo ir pirkimo sąskaitą.");
        if (r.Receivables.Concat(r.Payables).Any(l => l.Amount <= 0))
            throw new InvalidOperationException("Kiekvienos eilutės suma turi būti didesnė už 0.");
        if (r.Receivables.Concat(r.Payables).Any(l => decimal.Round(l.Amount, 2) != l.Amount))
            throw new InvalidOperationException("Sumos gali turėti ne daugiau kaip 2 skaitmenis po kablelio.");
        if (r.Receivables.GroupBy(l => l.DocumentId).Any(g => g.Count() > 1) || r.Payables.GroupBy(l => l.DocumentId).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Ta pati sąskaita pasirinkta kelis kartus.");
        if (r.Receivables.Sum(l => l.Amount) != r.Payables.Sum(l => l.Amount))
            throw new InvalidOperationException("Gautinų ir mokėtinų sumų suma turi būti lygi.");
    }

    private static string Placeholders(int count) => string.Join(", ", Enumerable.Range(0, Math.Max(count, 1)).Select(i => "@p" + i));

    private static async Task<string> NextNumberAsync(NordicBeesERPContext db, DateTime date)
    {
        var yy = date.ToString("yy");
        var prefix = $"UZS-{yy}-";
        var last = await RawSql.QueryAsync(db,
            "SELECT offset_number FROM settlement_offsets WHERE offset_number LIKE @p0 ORDER BY offset_number DESC LIMIT 1 FOR UPDATE",
            r => r.Str("offset_number"), default, prefix + "%");
        var next = 1;
        if (last.Count > 0 && int.TryParse(last[0]![prefix.Length..], out var n)) next = n + 1;
        return $"{prefix}{next:D4}";
    }

    /// <summary>Same status rule as ExpenseService.RecalculateInvoiceStatusAsync, but on the caller's context/transaction.</summary>
    private static async Task RecalculateExpenseAsync(NordicBeesERPContext db, int invoiceId)
    {
        var invoice = await db.ExpenseInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice == null) return;
        var paid = await db.ExpensePayments.AsNoTracking().Where(p => p.InvoiceId == invoiceId).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var status = ExpenseStatusHelper.Recalculate(paid, invoice.AmountInclVat, invoice.DueDate, invoice.Status);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE expense_invoices SET paid_amount = {0}, status = {1}, updated_at = {2} WHERE id = {3}",
            paid, status, DateTime.UtcNow, invoiceId);
    }

    // ───────────────────────── cancel ─────────────────────────

    public async Task CancelAsync(int id, string reason, int userId)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Nurodykite atšaukimo priežastį.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            await RawSql.QueryAsync(db, "SELECT id FROM settlement_offsets WHERE id = @p0 FOR UPDATE", r => r.Int("id"), default, id);
            var offset = await db.SettlementOffsets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("Užskaitos aktas nerastas.");
            if (offset.Status != SettlementOffset.StatusConfirmed)
                throw new InvalidOperationException($"Aktas {offset.OffsetNumber} jau atšauktas.");

            var lines = await db.SettlementOffsetLines.AsNoTracking().Where(l => l.OffsetId == id).ToListAsync();
            var now = DateTime.UtcNow;

            // Same lock order as confirm (invoices, then expense invoices, ascending ids) so a concurrent payment cannot deadlock the cancel.
            var lockInvoices = lines.Where(l => l.InvoiceId != null).Select(l => l.InvoiceId!.Value).OrderBy(x => x).ToList();
            var lockExpenses = lines.Where(l => l.ExpenseInvoiceId != null).Select(l => l.ExpenseInvoiceId!.Value).OrderBy(x => x).ToList();
            if (lockInvoices.Count > 0)
                await RawSql.QueryAsync(db, "SELECT id FROM invoices WHERE id IN (" + Placeholders(lockInvoices.Count) + ") ORDER BY id FOR UPDATE",
                    r => r.Int("id"), default, lockInvoices.Cast<object?>().ToArray());
            if (lockExpenses.Count > 0)
                await RawSql.QueryAsync(db, "SELECT id FROM expense_invoices WHERE id IN (" + Placeholders(lockExpenses.Count) + ") ORDER BY id FOR UPDATE",
                    r => r.Int("id"), default, lockExpenses.Cast<object?>().ToArray());

            foreach (var l in lines.Where(l => l.PaymentId != null))
            {
                // Keep the creation audit trail: detach it from the payment that is about to be deleted.
                await db.Database.ExecuteSqlRawAsync("UPDATE payment_audit_log SET payment_id = NULL WHERE payment_id = {0}", l.PaymentId!.Value);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM payment_allocations WHERE payment_id = {0}", l.PaymentId.Value);
                await db.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE id = {0} AND source = {1}", l.PaymentId.Value, Source);
                await PaymentService.RecalculateInvoiceStatusInternalAsync(db, l.InvoiceId!.Value);
                db.PaymentAuditLogs.Add(new PaymentAuditLog
                {
                    PaymentId = null, InvoiceId = l.InvoiceId, Action = "offset_cancel", OldAmount = l.Amount, ChangedBy = userId, ChangedAt = now,
                    Notes = $"Užskaita {offset.OffsetNumber} atšaukta: {reason}"
                });
                await db.SaveChangesAsync();
            }
            foreach (var l in lines.Where(l => l.ExpensePaymentId != null))
            {
                await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_payments WHERE id = {0} AND source = {1}", l.ExpensePaymentId!.Value, Source);
                await RecalculateExpenseAsync(db, l.ExpenseInvoiceId!.Value);
            }

            await db.Database.ExecuteSqlRawAsync(
                "UPDATE settlement_offsets SET status = {0}, cancelled_by = {1}, cancelled_at = {2}, cancel_reason = {3} WHERE id = {4}",
                SettlementOffset.StatusCancelled, userId, now, reason.Trim(), id);
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE settlement_offset_lines SET payment_id = NULL, expense_payment_id = NULL WHERE offset_id = {0}", id);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
