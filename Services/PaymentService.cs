// =====================================================
// NORDIC BEES ERP - PAYMENT SERVICE IMPLEMENTATION
// Framework: .NET 10
// =====================================================

using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;
using System;
using System.Text.Json;
using System.Globalization;

namespace NordicBeesERP.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

        public PaymentService(IDbContextFactory<NordicBeesERPContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // =====================================================
        // PAYMENT REGISTRATION
        // =====================================================

        public async Task<int> RegisterPaymentAsync(
            List<int> invoiceIds,
            decimal amount,
            DateTime paymentDate,
            string method,
            string? reference,
            string? notes,
            int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Validate invoices exist and get their details
            var invoices = await context.Invoices
                .Where(i => invoiceIds.Contains(i.Id))
                .Include(i => i.Customer)
                .ToListAsync();

            if (invoices.Count != invoiceIds.Count)
            {
                throw new InvalidOperationException("One or more invoices not found");
            }

            // Validate amounts - check each invoice can be paid
            foreach (var invoice in invoices)
            {
                var remaining = invoice.TotalInclVat - invoice.PaidAmount;
                if (remaining <= 0)
                {
                    throw new InvalidOperationException($"Invoice {invoice.Id} is already fully paid");
                }
            }

            // Get customer ID from first invoice (all invoices should have same customer)
            var firstInvoice = invoices[0];
            var customerId = firstInvoice.CustomerId;

            // Create payment with invoice_id set when there's exactly one invoice
            var payment = new Payment
            {
                PaymentDate = paymentDate,
                InvoiceId = invoiceIds.Count == 1 ? invoiceIds[0] : null,
                CustomerId = customerId,
                Amount = amount,
                PaymentMethod = method switch {
                    "bank_transfer" => PaymentMethod.BankTransfer,
                    "cash" => PaymentMethod.Cash,
                    "card" => PaymentMethod.Card,
                    "other" => PaymentMethod.Other,
                    _ => PaymentMethod.BankTransfer
                },
                ReferenceNumber = reference,
                Notes = notes,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            // Create allocations
            decimal remainingAmount = amount;
            foreach (var invoice in invoices)
            {
                var remainingOnInvoice = invoice.TotalInclVat - invoice.PaidAmount;
                var allocateAmount = Math.Min(remainingAmount, remainingOnInvoice);

                if (allocateAmount > 0)
                {
                    var allocation = new PaymentAllocation
                    {
                        PaymentId = payment.Id,
                        InvoiceId = invoice.Id,
                        AllocatedAmount = allocateAmount,
                        AllocatedAt = DateTime.UtcNow
                    };

                    context.PaymentAllocations.Add(allocation);
                    remainingAmount -= allocateAmount;
                }

                if (remainingAmount <= 0) break;
            }

            await context.SaveChangesAsync();

            // Recalculate invoice statuses using the SAME context
            foreach (var invoice in invoices)
            {
                await RecalculateInvoiceStatusInternalAsync(context, invoice.Id);
            }

            // Log audit entry
            await LogAuditEntryAsync(context, payment.Id, null, "create", null, payment.Amount, userId, JsonSerializer.Serialize(new
            {
                payment.PaymentDate,
                payment.Amount,
                payment.PaymentMethod,
                payment.ReferenceNumber,
                Allocations = invoices.Select((inv, i) => new
                {
                    InvoiceId = inv.Id,
                    AllocatedAmount = amount
                })
            }));

            await context.SaveChangesAsync();

            return payment.Id;
        }

        // =====================================================
        // INVOICE STATUS MANAGEMENT
        // =====================================================

        public async Task RecalculateInvoiceStatusAsync(int invoiceId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await RecalculateInvoiceStatusInternalAsync(context, invoiceId);
            await context.SaveChangesAsync();
        }

        public async Task RecalculateInvoiceStatusAsync(List<int> invoiceIds)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            foreach (var invoiceId in invoiceIds)
            {
                await RecalculateInvoiceStatusInternalAsync(context, invoiceId);
            }
            await context.SaveChangesAsync();
        }

        private async Task RecalculateInvoiceStatusInternalAsync(NordicBeesERPContext context, int invoiceId)
        {
            var totalAllocated = await context.PaymentAllocations
                .Where(a => a.InvoiceId == invoiceId)
                .SumAsync(a => a.AllocatedAmount);

            var invoice = await context.Invoices.FindAsync(invoiceId);
            if (invoice == null) return;

            var status = totalAllocated == 0 ? "unpaid"
                : totalAllocated >= invoice.TotalInclVat ? "paid"
                : "partial";

            var lastAllocationDate = await context.PaymentAllocations
                .Where(a => a.InvoiceId == invoiceId)
                .OrderByDescending(a => a.AllocatedAt)
                .Select(a => a.AllocatedAt)
                .FirstOrDefaultAsync();

            if (lastAllocationDate == null || lastAllocationDate == default(DateTime))
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE invoices SET paid_amount = {0}, payment_status = {1}, last_payment_date = NULL, updated_at = {2} WHERE id = {3}",
                    totalAllocated, status, DateTime.UtcNow, invoiceId);
            }
            else
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE invoices SET paid_amount = {0}, payment_status = {1}, last_payment_date = {2}, updated_at = {3} WHERE id = {4}",
                    totalAllocated, status, lastAllocationDate, DateTime.UtcNow, invoiceId);
            }
        }

        // =====================================================
        // UNPAID INVOICES
        // =====================================================

        public async Task<List<InvoiceWithPaymentInfo>> GetUnpaidInvoicesAsync(
            int? customerId = null,
            string? status = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = from i in context.Invoices
                        join bpRaw in context.BusinessPartners on i.CustomerId equals bpRaw.Id into bpGroup
                        from bp in bpGroup.DefaultIfEmpty()
                        where i.Status != InvoiceStatus.Disputed
                            && EF.Functions.Like(i.InvoiceNumber, "LAK%")
                            && !EF.Functions.Like(i.InvoiceNumber, "ULAK%")
                            && i.PaymentStatus != "paid"
                            && (i.TotalInclVat - i.PaidAmount) > 0
                        select new InvoiceWithPaymentInfo
                        {
                            Id = i.Id,
                            InvoiceNumber = i.InvoiceNumber,
                            InvoiceDate = i.InvoiceDate,
                            DueDate = i.DueDate,
                            CustomerId = i.CustomerId,
                            CustomerName = bp.Name,
                            TotalInclVat = i.TotalInclVat,
                            SubtotalExclVat = i.SubtotalExclVat,
                            TotalVat = i.TotalVat,
                            PaidAmount = i.PaidAmount,
                            RemainingAmount = i.TotalInclVat - i.PaidAmount,
                            PaymentStatus = i.PaymentStatus,
                            LastPaymentDate = i.LastPaymentDate
                        };

            if (customerId.HasValue)
            {
                query = query.Where(i => i.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(i => i.PaymentStatus == status);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate <= toDate.Value);
            }

            return await query.OrderBy(i => i.DueDate).ToListAsync();
        }

        // =====================================================
        // CASH FLOW FORECAST
        // =====================================================

        public async Task<List<CashFlowWeek>> GetCashFlowForecastAsync(int weeks = 8)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var result = new List<CashFlowWeek>();
            var today = DateTime.Today;
            var weekStart = today;

            // Find the start of the current week (Monday)
            while (weekStart.DayOfWeek != DayOfWeek.Monday)
            {
                weekStart = weekStart.AddDays(-1);
            }

            for (int i = 0; i < weeks; i++)
            {
                var weekEnd = weekStart.AddDays(6);
                var weekNumber = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                    weekStart, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);

                var invoices = await (from inv in context.Invoices
                                      join bpRaw in context.BusinessPartners on inv.CustomerId equals bpRaw.Id into bpGroup
                                      from bp in bpGroup.DefaultIfEmpty()
                                      where inv.DueDate >= weekStart && inv.DueDate <= weekEnd &&
                                            inv.PaymentStatus != "paid" && inv.Status != InvoiceStatus.Disputed
                                      orderby inv.DueDate
                                      select new InvoiceWithPaymentInfo
                                       {
                                           Id = inv.Id,
                                           InvoiceNumber = inv.InvoiceNumber,
                                           InvoiceDate = inv.InvoiceDate,
                                           DueDate = inv.DueDate,
                                           CustomerId = inv.CustomerId,
                                           CustomerName = bp.Name,
                                           TotalInclVat = inv.TotalInclVat,
                                           SubtotalExclVat = inv.SubtotalExclVat,
                                           TotalVat = inv.TotalVat,
                                           PaidAmount = inv.PaidAmount,
                                           RemainingAmount = inv.TotalInclVat - inv.PaidAmount,
                                          PaymentStatus = inv.PaymentStatus,
                                          LastPaymentDate = inv.LastPaymentDate
                                      })
                                      .ToListAsync();

                result.Add(new CashFlowWeek
                {
                    WeekNumber = weekNumber,
                    WeekStart = weekStart,
                    WeekEnd = weekEnd,
                    ExpectedIncome = invoices.Sum(i => i.RemainingAmount),
                    InvoiceCount = invoices.Count,
                    Invoices = invoices
                });

                weekStart = weekStart.AddDays(7);
            }

            return result;
        }

        // =====================================================
        // AGING REPORT
        // =====================================================

        public async Task<AgingReport> GetAgingReportAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var today = DateTime.Today;
            var report = new AgingReport();

            // Get ALL unpaid LAK invoices (excluding ULAK, disputed) with remaining amount > 0
            // This is used to calculate the total overdue summary
            var unpaidInvoices = await (from i in context.Invoices
                                        join bpRaw in context.BusinessPartners on i.CustomerId equals bpRaw.Id into bpGroup
                                        from bp in bpGroup.DefaultIfEmpty()
                                        where EF.Functions.Like(i.InvoiceNumber, "LAK%") &&
                                              !EF.Functions.Like(i.InvoiceNumber, "ULAK%") &&
                                              i.PaymentStatus != "paid" &&
                                              (i.TotalInclVat - i.PaidAmount) > 0 &&
                                              i.Status != InvoiceStatus.Disputed
                                        select new InvoiceWithPaymentInfo
                                        {
                                            Id = i.Id,
                                            InvoiceNumber = i.InvoiceNumber,
                                            InvoiceDate = i.InvoiceDate,
                                            DueDate = i.DueDate,
                                            CustomerId = i.CustomerId,
                                            CustomerName = bp.Name,
                                            TotalInclVat = i.TotalInclVat,
                                            SubtotalExclVat = i.SubtotalExclVat,
                                            TotalVat = i.TotalVat,
                                            PaidAmount = i.PaidAmount,
                                            RemainingAmount = i.TotalInclVat - i.PaidAmount,
                                            PaymentStatus = i.PaymentStatus,
                                            LastPaymentDate = i.LastPaymentDate
                                        })
                                        .ToListAsync();

            // Classify overdue invoices into buckets (only invoices past due date)
            var overdueInvoices = unpaidInvoices.Where(i => i.DueDate.HasValue && i.DueDate.Value < today).ToList();

            // Calculate totals from ONLY overdue invoices (sum of 4 aging buckets)
            report.TotalOverdue = overdueInvoices.Sum(i => i.RemainingAmount);
            report.TotalOverdueExclVat = overdueInvoices.Sum(i =>
                i.TotalInclVat > 0 ? i.RemainingAmount * i.SubtotalExclVat / i.TotalInclVat : 0);
            report.TotalOverdueVat = report.TotalOverdue - report.TotalOverdueExclVat;

            foreach (var invoice in overdueInvoices)
            {
                var daysOverdue = (today - invoice.DueDate!.Value).Days;

                if (daysOverdue <= 30)
                {
                    report.Bucket0To30.Invoices.Add(invoice);
                    report.Bucket0To30.TotalAmount += invoice.RemainingAmount;
                    report.Bucket0To30.InvoiceCount++;
                }
                else if (daysOverdue <= 60)
                {
                    report.Bucket31To60.Invoices.Add(invoice);
                    report.Bucket31To60.TotalAmount += invoice.RemainingAmount;
                    report.Bucket31To60.InvoiceCount++;
                }
                else if (daysOverdue <= 90)
                {
                    report.Bucket61To90.Invoices.Add(invoice);
                    report.Bucket61To90.TotalAmount += invoice.RemainingAmount;
                    report.Bucket61To90.InvoiceCount++;
                }
                else
                {
                    report.Bucket90Plus.Invoices.Add(invoice);
                    report.Bucket90Plus.TotalAmount += invoice.RemainingAmount;
                    report.Bucket90Plus.InvoiceCount++;
                }
            }

            return report;
        }

        // =====================================================
        // PAYMENT HISTORY
        // =====================================================

        public async Task<PaymentHistoryResult> GetPaymentHistoryAsync(
            int? customerId = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? paymentMethod = null,
            string? source = null,
            string? searchTerm = null,
            string? sortBy = null,
            string? sortDirection = null,
            int take = 50,
            int skip = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Default sort: newest payment first (OrderByDescending PaymentDate)
            // When sortBy is null or "paymentdate" and direction is not "asc", it uses OrderDescending

            var today = DateTime.Today;

            var query = from p in context.Payments
                        join bpRaw in context.BusinessPartners on p.CustomerId equals bpRaw.Id into bpGroup
                        from bp in bpGroup.DefaultIfEmpty()
                        join invRaw in context.Invoices on p.InvoiceId equals invRaw.Id into invGroup
                        from inv in invGroup.DefaultIfEmpty()
                        select new PaymentWithDetails
                        {
                            Id = p.Id,
                            PaymentDate = p.PaymentDate,
                            CustomerId = p.CustomerId,
                            CustomerName = bp.Name,
                            Amount = p.Amount,
                            PaymentMethod = p.PaymentMethod.ToString(),
                            ReferenceNumber = p.ReferenceNumber,
                            Notes = p.Notes,
                            CreatedAt = p.CreatedAt,
                            CreatedByName = null,
                            PaymentNumber = $"M-{p.PaymentDate.Year:D4}-{p.Id:D5}",
                            CanBeDeleted = true,
                            DueDate = inv == null ? null : inv.DueDate,
                            DaysFromDue = null,
                            InvoiceNumber = inv == null ? null : inv.InvoiceNumber,
                            InvoiceDate = inv == null ? null : inv.InvoiceDate
                        };

            if (customerId.HasValue)
            {
                query = query.Where(p => p.CustomerId == customerId.Value);
            }

            if (fromDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(p => p.PaymentDate <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(paymentMethod))
            {
                query = query.Where(p => p.PaymentMethod == paymentMethod);
            }

            // Filtravimas pagal paieškos terminą
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                var matchingIds = await context.Payments
                    .Include(p => p.Invoice)
                    .Where(p => 
                        (p.Invoice != null && p.Invoice.InvoiceNumber.ToLower().Contains(term)) ||
                        (p.ReferenceNumber != null && p.ReferenceNumber.ToLower().Contains(term)))
                    .Select(p => p.Id)
                    .ToListAsync();
                query = query.Where(p => 
                    matchingIds.Contains(p.Id) ||
                    (p.CustomerName != null && p.CustomerName.ToLower().Contains(term)));
            }

            // Apply sorting
            IOrderedQueryable<PaymentWithDetails> orderedQuery;
            var direction = string.IsNullOrEmpty(sortDirection) || sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
            
            switch (sortBy?.ToLower())
            {
                case "invoicedate":
                    orderedQuery = direction == "asc" 
                        ? query.OrderBy(p => p.InvoiceDate)
                        : query.OrderByDescending(p => p.InvoiceDate);
                    break;
                case "duedate":
                    orderedQuery = direction == "asc" 
                        ? query.OrderBy(p => p.DueDate)
                        : query.OrderByDescending(p => p.DueDate);
                    break;
                case "customername":
                    orderedQuery = direction == "asc" 
                        ? query.OrderBy(p => p.CustomerName)
                        : query.OrderByDescending(p => p.CustomerName);
                    break;
                case "amount":
                    orderedQuery = direction == "asc" 
                        ? query.OrderBy(p => p.Amount)
                        : query.OrderByDescending(p => p.Amount);
                    break;
                case "paymentdate":
                default:
                    orderedQuery = direction == "asc" 
                        ? query.OrderBy(p => p.PaymentDate)
                        : query.OrderByDescending(p => p.PaymentDate);
                    break;
            }

            var totalCount = await orderedQuery.CountAsync();
            var payments = await orderedQuery
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            // Calculate DaysFromDue in memory after DB query
            foreach (var payment in payments)
            {
                if (payment.DueDate.HasValue)
                    payment.DaysFromDue = (today - payment.DueDate.Value).Days;
            }

            // Load allocations and audit logs for each payment
            foreach (var payment in payments)
            {
                payment.Allocations = await context.PaymentAllocations
                    .Where(a => a.PaymentId == payment.Id)
                    .Join(context.Invoices,
                        a => a.InvoiceId,
                        i => i.Id,
                        (a, i) => new PaymentAllocationInfo
                        {
                            InvoiceId = i.Id,
                            InvoiceNumber = i.InvoiceNumber,
                            AllocatedAmount = a.AllocatedAmount,
                            AllocatedAt = a.AllocatedAt
                        })
                    .OrderBy(a => a.InvoiceNumber)
                    .ToListAsync();

                payment.AuditLogs = new List<AuditLogEntry>();
            }

            return new PaymentHistoryResult
            {
                Payments = payments,
                TotalCount = totalCount,
                PageNumber = skip / take + 1,
                PageSize = take,
                TotalPages = (int)Math.Ceiling(totalCount / (double)take)
            };
        }

        public async Task<PaymentWithDetails?> GetPaymentDetailAsync(int paymentId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var payment = await (from p in context.Payments
                                 join bpRaw in context.BusinessPartners on p.CustomerId equals bpRaw.Id into bpGroup
                                 from bp in bpGroup.DefaultIfEmpty()
                                 where p.Id == paymentId
                                 select new PaymentWithDetails
                                 {
                                     Id = p.Id,
                                     PaymentDate = p.PaymentDate,
                                     CustomerId = p.CustomerId,
                                     CustomerName = bp.Name,
                                     Amount = p.Amount,
                                     PaymentMethod = p.PaymentMethod.ToString(),
                                     ReferenceNumber = p.ReferenceNumber,
                                     Notes = p.Notes,
                                     CreatedAt = p.CreatedAt,
                                      CreatedByName = p.CreatedByNavigation != null ? p.CreatedByNavigation.FullName : null,
                                     PaymentNumber = $"M-{p.PaymentDate.Year:D4}-{p.Id:D5}",
                                     CanBeDeleted = !p.Allocations.Any(),
                                     DueDate = p.Invoice == null ? null : p.Invoice.DueDate,
                                     InvoiceNumber = p.Invoice == null ? null : p.Invoice.InvoiceNumber
                                 })
                                 .FirstOrDefaultAsync();

            if (payment == null)
            {
                return null;
            }

            payment.Allocations = await context.PaymentAllocations
                .Where(a => a.PaymentId == paymentId)
                .Join(context.Invoices,
                    a => a.InvoiceId,
                    i => i.Id,
                    (a, i) => new PaymentAllocationInfo
                    {
                        InvoiceId = i.Id,
                        InvoiceNumber = i.InvoiceNumber,
                        AllocatedAmount = a.AllocatedAmount,
                        AllocatedAt = a.AllocatedAt
                    })
                .OrderBy(a => a.InvoiceNumber)
                .ToListAsync();

             payment.AuditLogs = await context.PaymentAuditLogs
                 .Where(a => a.PaymentId == paymentId)
                 .Join(context.ErpUsers,
                     a => a.ChangedBy,
                     u => u.Id,
                     (a, u) => new AuditLogEntry
                     {
                         Id = a.Id,
                         Action = a.Action,
                         OldAmount = a.OldAmount,
                         NewAmount = a.NewAmount,
                         UserName = u.FullName ?? u.Email,
                         ChangedAt = a.ChangedAt
                     })
                 .OrderBy(a => a.ChangedAt)
                 .ToListAsync();

            return payment;
        }

        // =====================================================
        // PAYMENT DELETION
        // =====================================================

        public async Task<bool> DeletePaymentAsync(int paymentId, int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var payment = await context.Payments
                .Include(p => p.Allocations)
                .Include(p => p.AuditLogs)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return false;
            }

            // Get invoice IDs before deleting
            var invoiceIds = payment.Allocations.Select(a => a.InvoiceId).ToList();

            // Log deletion
            await LogAuditEntryAsync(context, paymentId, null, "delete",
                payment.Amount, null, userId, JsonSerializer.Serialize(new
                {
                    payment.PaymentDate,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.ReferenceNumber,
                    Allocations = payment.Allocations.Select(a => new { a.InvoiceId, a.AllocatedAmount })
                }));

            // Delete allocations
            context.PaymentAllocations.RemoveRange(payment.Allocations.ToList());

            // Delete audit logs
            context.PaymentAuditLogs.RemoveRange(payment.AuditLogs.ToList());

            // Delete payment
            context.Payments.Remove(payment);

            await context.SaveChangesAsync();

            // Recalculate invoice statuses using the SAME context
            // MUST be after SaveChanges so SUM(allocated_amount) reflects the deleted row
            foreach (var invoiceId in invoiceIds)
            {
                await RecalculateInvoiceStatusInternalAsync(context, invoiceId);
            }

            await context.SaveChangesAsync();
            return true;
        }

        // =====================================================
        // PAYMENT UPDATE
        // =====================================================

        public async Task<bool> UpdatePaymentAsync(int paymentId, decimal amount, DateTime date, string method, string? reference, string? notes, int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            // Get the payment and its allocations
            var payment = await context.Payments
                .Include(p => p.Allocations)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                return false;
            }

            // Get original values for audit
            var originalAmount = payment.Amount;
            var originalDate = payment.PaymentDate;
            var originalMethod = payment.PaymentMethod;
            var originalReference = payment.ReferenceNumber;
            var originalNotes = payment.Notes;

            // Update payment fields
            payment.Amount = amount;
            payment.PaymentDate = date;
            payment.PaymentMethod = method switch {
                "bank_transfer" => PaymentMethod.BankTransfer,
                "cash" => PaymentMethod.Cash,
                "card" => PaymentMethod.Card,
                "other" => PaymentMethod.Other,
                _ => PaymentMethod.BankTransfer
            };
            payment.ReferenceNumber = reference;
            payment.Notes = notes;
            payment.UpdatedAt = DateTime.UtcNow;

            // Log update
            await LogAuditEntryAsync(context, paymentId, payment.InvoiceId, "update",
                originalAmount, amount, userId, JsonSerializer.Serialize(new
                {
                    payment.PaymentDate,
                    payment.Amount,
                    payment.PaymentMethod,
                    payment.ReferenceNumber,
                    payment.Notes,
                    ChangedFields = new {
                        AmountChanged = originalAmount != amount,
                        DateChanged = originalDate != date,
                        MethodChanged = originalMethod != payment.PaymentMethod,
                        ReferenceChanged = originalReference != payment.ReferenceNumber,
                        NotesChanged = originalNotes != payment.Notes
                    }
                }));

            context.Payments.Update(payment);
            await context.SaveChangesAsync();

            // Update payment_allocations.allocated_amount AFTER SaveChanges
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE payment_allocations SET allocated_amount = {0} WHERE payment_id = {1}",
                amount, paymentId);

            // Recalculate all invoices linked to this payment
            var invoiceIds = await context.PaymentAllocations
                .Where(a => a.PaymentId == paymentId)
                .Select(a => a.InvoiceId)
                .ToListAsync();

            foreach (var invoiceId in invoiceIds)
            {
                await RecalculateInvoiceStatusInternalAsync(context, invoiceId);
            }
            return true;
        }

        // =====================================================
        // BANK IMPORT SUPPORT
        // =====================================================

        public async Task<List<BankImportRow>> GetUnmatchedBankImportRowsAsync(int bankImportId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            return await context.BankImportRows
                .Where(r => r.ImportId == bankImportId && r.MatchStatus == "unmatched")
                .OrderBy(r => r.RowDate)
                .ToListAsync();
        }

        public async Task<BankImportRow> MatchBankImportRowAsync(int bankImportRowId, int invoiceId, int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var row = await context.BankImportRows.FindAsync(bankImportRowId);
            if (row == null)
            {
                throw new InvalidOperationException("Bank import row not found");
            }

            var invoice = await context.Invoices.FindAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException("Invoice not found");
            }

            row.MatchedInvoiceId = invoiceId;
            row.MatchStatus = "manual_match";

            await context.SaveChangesAsync();
            return row;
        }

        public async Task<int> CreatePaymentFromBankImportAsync(int bankImportRowId, int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var row = await context.BankImportRows
                .Include(r => r.BankImport)
                .FirstOrDefaultAsync(r => r.Id == bankImportRowId);

            if (row == null || row.MatchedInvoiceId == null)
            {
                throw new InvalidOperationException("Bank import row or matched invoice not found");
            }

            var invoice = await context.Invoices.FindAsync(row.MatchedInvoiceId.Value);
            if (invoice == null)
            {
                throw new InvalidOperationException($"Invoice {row.MatchedInvoiceId} not found");
            }

            // Check if payment already exists for this invoice from bank import
            var existingPayment = await context.Payments
                .Where(p => p.InvoiceId == row.MatchedInvoiceId && p.Source == "bank_import")
                .FirstOrDefaultAsync();
            if (existingPayment != null)
            {
                row.MatchStatus = "already_paid";
                await context.SaveChangesAsync();
                throw new InvalidOperationException($"Sąskaita {row.MatchedInvoiceId} jau apmokėta iš banko importo");
            }

             // Create payment with invoice_id set
             var payment = new Payment
             {
                 PaymentDate = row.RowDate,
                 InvoiceId = row.MatchedInvoiceId!.Value,
                 CustomerId = invoice.CustomerId,
                Amount = row.Amount,
                PaymentMethod = PaymentMethod.BankTransfer,
                ReferenceNumber = row.Reference,
                Notes = $"Bank import: {row.BankImport.FileName}",
                BankImportRowId = bankImportRowId,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            context.Payments.Add(payment);
            await context.SaveChangesAsync();

            // Create allocation
            var allocation = new PaymentAllocation
            {
                PaymentId = payment.Id,
                InvoiceId = row.MatchedInvoiceId!.Value,
                AllocatedAmount = row.Amount,
                AllocatedAt = DateTime.UtcNow
            };

            context.PaymentAllocations.Add(allocation);
            await context.SaveChangesAsync();

            // Recalculate invoice status using the SAME context
            await RecalculateInvoiceStatusInternalAsync(context, row.MatchedInvoiceId.Value);

            // Update bank import row
            row.MatchStatus = "auto_match";
            row.PaymentId = payment.Id;
            await context.SaveChangesAsync();

            // Log audit entry
            await LogAuditEntryAsync(context, payment.Id, null, "create", null, payment.Amount, userId, JsonSerializer.Serialize(new
            {
                payment.PaymentDate,
                payment.Amount,
                BankImportRowId = bankImportRowId
            }));

            await context.SaveChangesAsync();

            return payment.Id;
        }

        // =====================================================
        // HELPER METHODS
        // =====================================================

        private async Task LogAuditEntryAsync(
            NordicBeesERPContext context,
            int paymentId,
            int? invoiceId,
            string action,
            decimal? oldAmount,
            decimal? newAmount,
            int userId,
            string? notes)
        {
            var auditLog = new PaymentAuditLog
            {
                PaymentId = paymentId,
                InvoiceId = invoiceId,
                Action = action,
                OldAmount = oldAmount,
                NewAmount = newAmount,
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                Notes = notes
            };

            context.PaymentAuditLogs.Add(auditLog);
        }

        // =====================================================
        // BANK IMPORT MANAGEMENT
        // =====================================================

        public async Task<List<BankImport>> GetBankImportsAsync(string? status = null, int take = 50, int skip = 0)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var query = context.BankImports
                .OrderByDescending(bi => bi.ImportDate)
                .AsQueryable();
            
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(bi => bi.Status == status);
            }

            return await query.Skip(skip).Take(take).ToListAsync();
        }

        public async Task<int> CreateBankImportAsync(string fileName, string fileHash, int totalRows, int userId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var bankImport = new BankImport
            {
                FileName = fileName,
                FileHash = fileHash,
                TotalRows = totalRows,
                MatchedRows = 0,
                UnmatchedRows = 0,
                ProcessedRows = 0,
                Status = "pending",
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.BankImports.Add(bankImport);
            await context.SaveChangesAsync();

            return bankImport.Id;
        }

        public async Task UpdateBankImportAsync(int importId, int totalRows)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE bank_imports SET total_rows = {0} WHERE id = {1}",
                totalRows, importId);
        }

        public async Task<BankImport?> GetBankImportWithRowsAsync(int bankImportId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            return await context.BankImports
                .Include(bi => bi.Rows)
                .FirstOrDefaultAsync(bi => bi.Id == bankImportId);
        }

        // =====================================================
        // SALES INVOICES (LAK prefix only)
        // =====================================================

        public async Task<InvoiceWithPaymentInfoResult> GetSalesInvoicesAsync(
            int take = 50,
            int skip = 0,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            InvoiceStatus? status = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = from i in context.Invoices
                        join bpRaw in context.BusinessPartners on i.CustomerId equals bpRaw.Id into bpGroup
                        from bp in bpGroup.DefaultIfEmpty()
                        where EF.Functions.Like(i.InvoiceNumber, "LAK%") &&
                              !EF.Functions.Like(i.InvoiceNumber, "ULAK%")
                        select new InvoiceWithPaymentInfo
                        {
                            Id = i.Id,
                            InvoiceNumber = i.InvoiceNumber,
                            InvoiceDate = i.InvoiceDate,
                            DueDate = i.DueDate,
                            CustomerId = i.CustomerId,
                            CustomerName = bp.Name,
                            TotalInclVat = i.TotalInclVat,
                            SubtotalExclVat = i.SubtotalExclVat,
                            TotalVat = i.TotalVat,
                            PaidAmount = i.PaidAmount,
                            RemainingAmount = i.TotalInclVat - i.PaidAmount,
                            PaymentStatus = i.PaymentStatus,
                            LastPaymentDate = i.LastPaymentDate,
                            Status = i.Status.ToString()
                        };

            if (fromDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(i => i.InvoiceDate <= toDate.Value);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchTerm) || 
                                         i.CustomerName.Contains(searchTerm));
            }

            if (status.HasValue)
            {
                query = query.Where(i => i.Status == status.Value.ToString());
            }

            var totalCount = await query.CountAsync();

            var invoices = await query
                .OrderByDescending(i => i.InvoiceDate)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            return new InvoiceWithPaymentInfoResult
            {
                Invoices = invoices,
                TotalCount = totalCount,
                PageNumber = skip / take + 1,
                PageSize = take,
                TotalPages = (int)Math.Ceiling(totalCount / (double)take)
            };
        }

        // =====================================================
        // PAYMENTS BY INVOICE
        // =====================================================

        public async Task<List<PaymentHistoryItem>> GetPaymentsByInvoiceAsync(int invoiceId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = from pa in context.PaymentAllocations
                        join p in context.Payments on pa.PaymentId equals p.Id
                        join bp in context.BusinessPartners on p.CustomerId equals bp.Id into bpGroup
                        from bp in bpGroup.DefaultIfEmpty()
                        where pa.InvoiceId == invoiceId
                        orderby p.PaymentDate descending
                        select new PaymentHistoryItem
                        {
                            PaymentId = p.Id,
                            Date = p.PaymentDate,
                            Amount = pa.AllocatedAmount,
                            Method = p.PaymentMethod.ToString(),
                            Reference = p.ReferenceNumber,
                            Notes = p.Notes
                        };

            return await query.ToListAsync();
        }

        // =====================================================
        // PAYMENTS DASHBOARD KPI
        // =====================================================

        public async Task<PaymentsDashboardKpi> GetPaymentsDashboardKpiAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var kpi = new PaymentsDashboardKpi();

            // Card 1: Bendra suma (total) - All invoices (LAK prefix only)
            var totalInvoices = await context.Invoices
                .Where(i => EF.Functions.Like(i.InvoiceNumber, "LAK%") && !EF.Functions.Like(i.InvoiceNumber, "ULAK%"))
                .Select(i => new { i.TotalInclVat, i.PaidAmount, i.SubtotalExclVat, i.TotalVat })
                .ToListAsync();

            kpi.TotalAmountExclVat = totalInvoices.Sum(i => i.SubtotalExclVat);
            kpi.TotalVat = totalInvoices.Sum(i => i.TotalVat);
            kpi.TotalAmount = totalInvoices.Sum(i => i.TotalInclVat);

            // Card 2: Nepilnai sumokėti (partial payments remaining) - invoices with partial status
            var partialInvoices = await context.Invoices
                .Where(i => EF.Functions.Like(i.InvoiceNumber, "LAK%") && !EF.Functions.Like(i.InvoiceNumber, "ULAK%") && i.PaymentStatus == "partial")
                .Select(i => new { i.TotalInclVat, i.PaidAmount, i.SubtotalExclVat, i.TotalVat })
                .ToListAsync();

            // Partial = remaining amount (TotalInclVat - PaidAmount) calculated proportionally
            var partialRemainingInclVat = partialInvoices.Sum(i => i.TotalInclVat - i.PaidAmount);
            var partialRemainingVat = partialInvoices.Sum(i => i.TotalInclVat > 0 
                ? i.TotalVat * (i.TotalInclVat - i.PaidAmount) / i.TotalInclVat 
                : 0);
            kpi.PartialAmount = partialRemainingInclVat - partialRemainingVat;
            kpi.PartialVat = partialRemainingVat;
            kpi.PartialAmountInclVat = partialRemainingInclVat;
            kpi.PartialPaymentsCount = partialInvoices.Count;

            // Card 3: Permokėjimai (overpayments) - payments with no invoice (InvoiceId is null) or over-allocated
            // Overpayments = payments that exceed invoice total ( InvoiceId is set but amount > remaining on invoice )
            var overpayments = await (from p in context.Payments
                                      join pa in context.PaymentAllocations on p.Id equals pa.PaymentId into paGroup
                                      from pa in paGroup
                                      join i in context.Invoices on pa.InvoiceId equals i.Id
                                      where p.InvoiceId == null || pa.AllocatedAmount > (i.TotalInclVat - i.PaidAmount + pa.AllocatedAmount)
                                      select new { p.Amount, pa.AllocatedAmount, pa.InvoiceId })
                                      .Distinct()
                                      .ToListAsync();
            
            // Simplified: count payments with no invoice_id (advance payments) or where allocated > remaining
            var paymentOverpayments = await (from p in context.Payments
                                             where p.InvoiceId == null
                                             select p.Amount)
                                             .ToListAsync();
            
            kpi.OverpaidAmount = paymentOverpayments.Sum();
            kpi.OverpaidCount = paymentOverpayments.Count;

            // Card 4: Skolos (debts) - unpaid invoices
            var unpaidInvoices = await context.Invoices
                .Where(i => EF.Functions.Like(i.InvoiceNumber, "LAK%") && !EF.Functions.Like(i.InvoiceNumber, "ULAK%") && i.PaymentStatus == "unpaid")
                .Select(i => new { i.TotalInclVat, i.PaidAmount, i.SubtotalExclVat, i.TotalVat })
                .ToListAsync();

            // BUG 1 FIX: Proportional SubtotalExclVat based on RemainingAmount
            kpi.TotalDebtExclVat = unpaidInvoices.Sum(i => i.TotalInclVat > 0 ? (i.TotalInclVat - i.PaidAmount) * i.SubtotalExclVat / i.TotalInclVat : 0);
            kpi.TotalDebtVat = unpaidInvoices.Sum(i => i.TotalInclVat > 0 ? (i.TotalInclVat - i.PaidAmount) * i.TotalVat / i.TotalInclVat : 0);
            kpi.TotalDebt = unpaidInvoices.Sum(i => i.TotalInclVat - i.PaidAmount);

            return kpi;
        }
    }
}
