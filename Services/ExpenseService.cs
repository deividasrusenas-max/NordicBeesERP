using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;
        private readonly IAuthService _authService;

        public ExpenseService(IDbContextFactory<NordicBeesERPContext> dbFactory, IAuthService authService)
        {
            _dbFactory = dbFactory;
            _authService = authService;
        }

        // =====================================================
        // INVOICES
        // =====================================================

        public async Task<List<ExpenseInvoice>> GetInvoicesAsync(string? status = null, int? supplierId = null, DateTime? fromDate = null, DateTime? toDate = null, int? categoryId = null)
        {
            await using var context = _dbFactory.CreateDbContext();
            var query = context.ExpenseInvoices.AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(i => i.Status == status);

            if (supplierId.HasValue)
                query = query.Where(i => i.SupplierId == supplierId.Value);

            if (fromDate.HasValue)
                query = query.Where(i => i.InvoiceDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(i => i.InvoiceDate <= toDate.Value);

            if (categoryId.HasValue)
            {
                // category_id filter not supported yet - ExpenseInvoiceLines is [NotMapped] navigation
                // TODO: implement when direct category_id on invoice is used
            }

            var invoices = await query
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            // Populate SupplierName from BusinessPartners table
            var supplierIds = invoices
                .Where(i => i.SupplierId.HasValue)
                .Select(i => i.SupplierId.Value)
                .Distinct()
                .ToList();

            if (supplierIds.Count > 0)
            {
                var suppliers = await context.BusinessPartners
                    .Where(s => supplierIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name);

                foreach (var invoice in invoices)
                {
                    if (invoice.SupplierId.HasValue && suppliers.TryGetValue(invoice.SupplierId.Value, out var supplierName))
                    {
                        invoice.SupplierName = supplierName;
                    }
                }
            }

            return invoices;
        }

        public async Task<ExpenseInvoice?> GetInvoiceWithDetailsAsync(int id)
        {
            await using var context = _dbFactory.CreateDbContext();
            var invoice = await context.ExpenseInvoices
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null) return null;
            if (invoice.SupplierId.HasValue)
            {
                var supplier = await context.BusinessPartners
                    .Where(s => s.Id == invoice.SupplierId.Value)
                    .Select(s => new { s.Name })
                    .FirstOrDefaultAsync();
                if (supplier != null) invoice.SupplierName = supplier.Name;
            }
            return invoice;
        }

        public async Task<InvoiceAddResult> CreateInvoiceAsync(ExpenseInvoice invoice)
        {
            using var context = _dbFactory.CreateDbContext();
            
            // Set default values
            if (invoice.Status == null)
                invoice.Status = "DRAFT";
            if (invoice.OcrStatus == null)
                invoice.OcrStatus = "PENDING";
            
            // Calculate totals if not set
            if (invoice.AmountExclVat == 0 && invoice.ExpenseInvoiceLines != null)
            {
                foreach (var line in invoice.ExpenseInvoiceLines)
                {
                    line.AmountInclVat = line.AmountExclVat * (1 + line.VatRate / 100);
                }
                invoice.AmountExclVat = invoice.ExpenseInvoiceLines.Sum(l => l.AmountExclVat);
                invoice.VatAmount = invoice.ExpenseInvoiceLines.Sum(l => l.AmountInclVat - l.AmountExclVat);
                invoice.AmountInclVat = invoice.AmountExclVat + invoice.VatAmount;
            }

            invoice.CreatedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;

            // Check for duplicates BEFORE saving
            // Only exclude current invoice if it's an existing one (Id != 0)
            var supplierName = invoice.SupplierId.HasValue
                ? await context.BusinessPartners
                    .Where(b => b.Id == invoice.SupplierId)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync() ?? ""
                : invoice.PendingSupplierName ?? "";

            var duplicate = await context.ExpenseInvoices
                .Where(i => i.InvoiceNumber == invoice.InvoiceNumber
                         && i.InvoiceNumber != null
                         && i.InvoiceNumber != "")
                .Where(i => i.SupplierId == invoice.SupplierId
                         || (i.SupplierId == null 
                             && !string.IsNullOrEmpty(supplierName)
                             && i.PendingSupplierName == supplierName))
                .FirstOrDefaultAsync();

            if (invoice.Id != 0)
            {
                duplicate = await context.ExpenseInvoices
                    .Where(i => i.InvoiceNumber == invoice.InvoiceNumber
                             && i.InvoiceNumber != null
                             && i.InvoiceNumber != "")
                    .Where(i => i.SupplierId == invoice.SupplierId
                             || (i.SupplierId == null 
                                 && !string.IsNullOrEmpty(supplierName)
                                 && i.PendingSupplierName == supplierName))
                    .Where(i => i.Id != invoice.Id)
                    .FirstOrDefaultAsync();
            }
            
            if (duplicate != null)
            {
                // Save as DUPLICATE_PENDING status instead of normal flow
                invoice.Status = "DUPLICATE_PENDING";
                invoice.DuplicateOfId = duplicate.Id;
                await context.ExpenseInvoices.AddAsync(invoice);
                await context.SaveChangesAsync();
                
                // Log duplicate detection
                await LogAuditAsync(context, invoice.Id, invoice.InvoiceNumber, "DUPLICATE_DETECTED", 
                    $"Duplicate of invoice #{duplicate.InvoiceNumber}");
                
                return new InvoiceAddResult { IsDuplicate = true, OriginalInvoiceId = duplicate.Id, ThisInvoiceId = invoice.Id };
            }

            await context.ExpenseInvoices.AddAsync(invoice);
            await context.SaveChangesAsync();
            
            // Save invoice lines if provided
            if (invoice.ExpenseInvoiceLines != null && invoice.ExpenseInvoiceLines.Any())
            {
                var lines = invoice.ExpenseInvoiceLines.Select((l, i) => new ExpenseInvoiceLine
                {
                    InvoiceId = invoice.Id,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    AmountExclVat = l.AmountExclVat,
                    VatRate = l.VatRate,
                    AmountInclVat = l.AmountInclVat,
                    SortOrder = i
                }).ToList();
                
                await context.ExpenseInvoiceLines.AddRangeAsync(lines);
                await context.SaveChangesAsync();
                
                // Update invoice totals based on lines
                invoice.AmountExclVat = lines.Sum(l => l.AmountExclVat);
                invoice.VatAmount = lines.Sum(l => l.AmountInclVat - l.AmountExclVat);
                invoice.AmountInclVat = lines.Sum(l => l.AmountInclVat);
                context.Entry(invoice).Property(i => i.AmountExclVat).IsModified = true;
                context.Entry(invoice).Property(i => i.VatAmount).IsModified = true;
                context.Entry(invoice).Property(i => i.AmountInclVat).IsModified = true;
                await context.SaveChangesAsync();
            }
            
            // Log invoice creation
            await LogAuditAsync(context, invoice.Id, invoice.InvoiceNumber, "UPLOADED");
            
            return new InvoiceAddResult { IsDuplicate = false, OriginalInvoiceId = 0, ThisInvoiceId = invoice.Id };
        }

        public async Task<ExpenseInvoice> UpdateInvoiceAsync(ExpenseInvoice invoice, List<string>? overriddenFlags = null)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(invoice).Property(i => i.InvoiceNumber).IsModified = true;
            context.Entry(invoice).Property(i => i.InvoiceDate).IsModified = true;
            context.Entry(invoice).Property(i => i.DueDate).IsModified = true;
            context.Entry(invoice).Property(i => i.AmountExclVat).IsModified = true;
            context.Entry(invoice).Property(i => i.VatRate).IsModified = true;
            context.Entry(invoice).Property(i => i.VatAmount).IsModified = true;
            context.Entry(invoice).Property(i => i.AmountInclVat).IsModified = true;
            context.Entry(invoice).Property(i => i.Notes).IsModified = true;
            context.Entry(invoice).Property(i => i.Status).IsModified = true;
            context.Entry(invoice).Property(i => i.UpdatedAt).IsModified = true;

            invoice.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            
            // Recalculate OCR flags after saving invoice changes
            var lines = await context.ExpenseInvoiceLines.Where(l => l.InvoiceId == invoice.Id).ToListAsync();
            
            var flags = new List<string>();
            if (string.IsNullOrEmpty(invoice.InvoiceNumber)) flags.Add(OcrFlag.MissingInvNumber);
            if (invoice.AmountInclVat == 0) flags.Add(OcrFlag.MissingAmount);
            if (invoice.VatRate == 0 && invoice.AmountInclVat > 0) flags.Add(OcrFlag.ZeroVat);
            if (lines.Count == 0) flags.Add(OcrFlag.LinesNotFound);
            if (lines.Count > 0 && Math.Abs(lines.Sum(l => l.AmountInclVat) - invoice.AmountInclVat) > 0.01m)
                flags.Add(OcrFlag.AmountMismatch);
            
            // Keep non-recalculable flags from existing
            var existing = string.IsNullOrEmpty(invoice.OcrFlags) 
                ? new List<string>() 
                : System.Text.Json.JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();
            bool wrongRecipientDismissed = overriddenFlags != null && !overriddenFlags.Contains(OcrFlag.WrongRecipient);
            if (!wrongRecipientDismissed && existing.Contains(OcrFlag.WrongRecipient))
                flags.Add(OcrFlag.WrongRecipient);
            if (existing.Contains(OcrFlag.ViesUnavailable)) flags.Add(OcrFlag.ViesUnavailable);
            if (existing.Contains(OcrFlag.VendorNotFound) && invoice.SupplierId == null) flags.Add(OcrFlag.VendorNotFound);
            if (existing.Contains(OcrFlag.Duplicate)) flags.Add(OcrFlag.Duplicate);
            
            // Update flags
            invoice.OcrFlags = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            
            // Update status if no more critical flags
            if (invoice.Status == "NEEDS_REVIEW" && !flags.Any(f => 
                f == OcrFlag.WrongRecipient || f == OcrFlag.MissingAmount || f == OcrFlag.AmountMismatch || f == OcrFlag.LowConfidence))
            {
                invoice.Status = invoice.SupplierId == null ? "PENDING_SUPPLIER" : "PENDING";
                context.Entry(invoice).Property(i => i.Status).IsModified = true;
            }
            
            context.Update(invoice);
            await context.SaveChangesAsync();
            
            return invoice;
        }

        public async Task<bool> DeleteInvoiceAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices.FindAsync(id);
            if (invoice == null)
                return false;

            context.ExpenseInvoices.Remove(invoice);
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // INVOICE LINES
        // =====================================================

        public async Task<List<ExpenseInvoiceLine>> GetInvoiceLinesAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.ExpenseInvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .OrderBy(l => l.SortOrder)
                .ToListAsync();
        }

        public async Task<ExpenseInvoiceLine> AddInvoiceLineAsync(ExpenseInvoiceLine line)
        {
            using var context = _dbFactory.CreateDbContext();
            
            // Calculate totals
            line.AmountInclVat = line.AmountExclVat * (1 + line.VatRate / 100);
            
            await context.ExpenseInvoiceLines.AddAsync(line);
            await context.SaveChangesAsync();
            
            return line;
        }

        public async Task<ExpenseInvoiceLine> UpdateInvoiceLineAsync(ExpenseInvoiceLine line)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(line).Property(l => l.Description).IsModified = true;
            context.Entry(line).Property(l => l.AmountExclVat).IsModified = true;
            context.Entry(line).Property(l => l.VatRate).IsModified = true;
            context.Entry(line).Property(l => l.AmountInclVat).IsModified = true;
            context.Entry(line).Property(l => l.SortOrder).IsModified = true;

            // Recalculate totals
            line.AmountInclVat = line.AmountExclVat * (1 + line.VatRate / 100);
            
            await context.SaveChangesAsync();
            
            return line;
        }

        public async Task<bool> DeleteInvoiceLineAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var line = await context.ExpenseInvoiceLines.FindAsync(id);
            if (line == null)
                return false;

            context.ExpenseInvoiceLines.Remove(line);
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // ALLOCATIONS
        // =====================================================

        public async Task<List<ExpenseLineAllocation>> GetAllocationsAsync(int invoiceLineId)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.ExpenseLineAllocations
                .Where(a => a.InvoiceLineId == invoiceLineId)
                .ToListAsync();
        }

        public async Task<ExpenseLineAllocation> AddAllocationAsync(ExpenseLineAllocation allocation)
        {
            using var context = _dbFactory.CreateDbContext();
            
            await context.ExpenseLineAllocations.AddAsync(allocation);
            await context.SaveChangesAsync();
            
            return allocation;
        }

        public async Task<ExpenseLineAllocation> UpdateAllocationAsync(ExpenseLineAllocation allocation)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(allocation).Property(a => a.CategoryId).IsModified = true;
            context.Entry(allocation).Property(a => a.CostCenterId).IsModified = true;
            context.Entry(allocation).Property(a => a.AllocatedAmount).IsModified = true;
            context.Entry(allocation).Property(a => a.AllocatedPercent).IsModified = true;

            await context.SaveChangesAsync();
            
            return allocation;
        }

        public async Task<bool> DeleteAllocationAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var allocation = await context.ExpenseLineAllocations.FindAsync(id);
            if (allocation == null)
                return false;

            context.ExpenseLineAllocations.Remove(allocation);
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // PAYMENTS
        // =====================================================

        public async Task<List<ExpensePayment>> GetPaymentsAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.ExpensePayments
                .Where(p => p.InvoiceId == invoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        public async Task<ExpensePayment> AddPaymentAsync(ExpensePayment payment)
        {
            using var context = _dbFactory.CreateDbContext();
            
            // Get invoice info before payment is added
            var invoice = await context.ExpenseInvoices.FindAsync(payment.InvoiceId);
            var oldStatus = invoice?.Status;
            
            payment.CreatedAt = DateTime.UtcNow;
            await context.ExpensePayments.AddAsync(payment);
            await context.SaveChangesAsync();
            
            // Recalculate paid amount and update status
            await RecalculateInvoiceStatusAsync(payment.InvoiceId);
            
            // Get new status after recalculation
            await context.Entry(invoice!).ReloadAsync();
            var newStatus = invoice!.Status;
            
            // Log payment added
            await LogAuditAsync(context, payment.InvoiceId, invoice.InvoiceNumber, "PAYMENT_ADDED", 
                $"Amount: {payment.Amount:C}", oldStatus, newStatus);
            
            return payment;
        }

        public async Task<bool> DeletePaymentAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var payment = await context.ExpensePayments.FindAsync(id);
            if (payment == null)
                return false;

            var invoiceId = payment.InvoiceId;
            
            // Get invoice info before payment is deleted
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            var oldStatus = invoice?.Status;
            var invoiceNumber = invoice?.InvoiceNumber ?? "Unknown";
            
            context.ExpensePayments.Remove(payment);
            await context.SaveChangesAsync();
            
            // Recalculate paid amount and update status
            await RecalculateInvoiceStatusAsync(invoiceId);
            
            // Get new status after recalculation
            await context.Entry(invoice!).ReloadAsync();
            var newStatus = invoice!.Status;
            
            // Log payment deleted
            await LogAuditAsync(context, invoiceId, invoiceNumber, "PAYMENT_DELETED", 
                $"Amount: {payment.Amount:C}", oldStatus, newStatus);
            
            return true;
        }

        public async Task<ExpensePayment> UpdatePaymentAsync(ExpensePayment payment)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var existing = await context.ExpensePayments.FindAsync(payment.Id);
            if (existing == null)
                throw new InvalidOperationException($"Payment with ID {payment.Id} not found");

            var oldStatus = existing.Invoice != null ? existing.Invoice.Status : null;
            var invoiceId = existing.InvoiceId;

            existing.PaymentDate = payment.PaymentDate;
            existing.Amount = payment.Amount;
            existing.PaymentMethod = payment.PaymentMethod;
            existing.Reference = payment.Reference;
            existing.Notes = payment.Notes;

            context.Entry(existing).Property(p => p.PaymentDate).IsModified = true;
            context.Entry(existing).Property(p => p.Amount).IsModified = true;
            context.Entry(existing).Property(p => p.PaymentMethod).IsModified = true;
            context.Entry(existing).Property(p => p.Reference).IsModified = true;
            context.Entry(existing).Property(p => p.Notes).IsModified = true;

            await context.SaveChangesAsync();
            
            // Recalculate invoice paid amount and status
            await RecalculateInvoiceStatusAsync(invoiceId);
            
            return existing;
        }

        // =====================================================
        // BUDGETS
        // =====================================================

        public async Task<List<ExpenseBudget>> GetBudgetsAsync(int? categoryId = null, int? year = null)
        {
            using var context = _dbFactory.CreateDbContext();
            var query = context.ExpenseBudgets.AsQueryable();

            if (categoryId.HasValue)
                query = query.Where(b => b.CategoryId == categoryId.Value);

            if (year.HasValue)
                query = query.Where(b => b.Year == year.Value);

            return await query
                .OrderBy(b => b.CategoryId)
                .ThenBy(b => b.Year)
                .ThenBy(b => b.Month)
                .ToListAsync();
        }

        public async Task<ExpenseBudget> AddBudgetAsync(ExpenseBudget budget)
        {
            using var context = _dbFactory.CreateDbContext();
            
            await context.ExpenseBudgets.AddAsync(budget);
            await context.SaveChangesAsync();
            
            return budget;
        }

        public async Task<ExpenseBudget> UpdateBudgetAsync(ExpenseBudget budget)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(budget).Property(b => b.PlannedAmount).IsModified = true;

            await context.SaveChangesAsync();
            
            return budget;
        }

        public async Task<bool> DeleteBudgetAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var budget = await context.ExpenseBudgets.FindAsync(id);
            if (budget == null)
                return false;

            context.ExpenseBudgets.Remove(budget);
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // CATEGORIES
        // =====================================================

        public async Task<List<ExpenseCategory>> GetCategoriesAsync(bool? isActive = null)
        {
            using var context = _dbFactory.CreateDbContext();
            var query = context.ExpenseCategories.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            return await query
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<ExpenseCategory> AddCategoryAsync(ExpenseCategory category)
        {
            using var context = _dbFactory.CreateDbContext();
            
            await context.ExpenseCategories.AddAsync(category);
            await context.SaveChangesAsync();
            
            return category;
        }

        public async Task<ExpenseCategory> UpdateCategoryAsync(ExpenseCategory category)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(category).Property(c => c.Name).IsModified = true;
            context.Entry(category).Property(c => c.Code).IsModified = true;
            context.Entry(category).Property(c => c.ParentId).IsModified = true;
            context.Entry(category).Property(c => c.IsActive).IsModified = true;
            context.Entry(category).Property(c => c.SortOrder).IsModified = true;

            await context.SaveChangesAsync();
            
            return category;
        }

        public async Task<bool> ToggleCategoryActiveAsync(int id, bool isActive)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var category = await context.ExpenseCategories.FindAsync(id);
            if (category == null)
                return false;

            category.IsActive = isActive;
            context.Entry(category).Property(c => c.IsActive).IsModified = true;
            await context.SaveChangesAsync();
            
            return true;
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var category = await context.ExpenseCategories.FindAsync(id);
            if (category == null)
                return false;

            // Soft delete: set is_active = false
            category.IsActive = false;
            context.Entry(category).Property(c => c.IsActive).IsModified = true;
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // COST CENTERS
        // =====================================================

        public async Task<List<ExpenseCostCenter>> GetCostCentersAsync(bool? isActive = null)
        {
            using var context = _dbFactory.CreateDbContext();
            var query = context.ExpenseCostCenters.AsQueryable();

            if (isActive.HasValue)
                query = query.Where(c => c.IsActive == isActive.Value);

            return await query
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<ExpenseCostCenter> AddCostCenterAsync(ExpenseCostCenter center)
        {
            using var context = _dbFactory.CreateDbContext();
            
            await context.ExpenseCostCenters.AddAsync(center);
            await context.SaveChangesAsync();
            
            return center;
        }

        public async Task<ExpenseCostCenter> UpdateCostCenterAsync(ExpenseCostCenter center)
        {
            using var context = _dbFactory.CreateDbContext();
            
            context.Entry(center).Property(c => c.Name).IsModified = true;
            context.Entry(center).Property(c => c.Code).IsModified = true;
            context.Entry(center).Property(c => c.IsActive).IsModified = true;

            await context.SaveChangesAsync();
            
            return center;
        }

        public async Task<bool> DeleteCostCenterAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var center = await context.ExpenseCostCenters.FindAsync(id);
            if (center == null)
                return false;

            context.ExpenseCostCenters.Remove(center);
            await context.SaveChangesAsync();
            
            return true;
        }

        // =====================================================
        // CALCULATIONS
        // =====================================================

        public async Task<ExpenseInvoice?> GetInvoiceAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.ExpenseInvoices.FindAsync(id);
        }

        public async Task RecalculateInvoiceStatusAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null)
                return;

            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;

            var totalPayments = await context.ExpensePayments
                .Where(p => p.InvoiceId == invoiceId)
                .SumAsync(p => p.Amount);

            invoice.PaidAmount = totalPayments;

            // Perduodame currentStatus, kad neprarastume APPROVED žymės
            invoice.Status = ExpenseStatusHelper.Recalculate(
                totalPayments,
                invoice.AmountInclVat,
                invoice.DueDate,
                invoice.Status);

            // Mark all modified properties explicitly
            context.Entry(invoice).Property(i => i.PaidAmount).IsModified = true;
            context.Entry(invoice).Property(i => i.Status).IsModified = true;
            context.Entry(invoice).Property(i => i.UpdatedAt).IsModified = true;
            invoice.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
            
            // Log status change if it changed
            if (oldStatus != invoice.Status)
            {
                await LogAuditAsync(context, invoiceId, invoiceNumber, "STATUS_CHANGED", 
                    $"Status changed from {oldStatus} to {invoice.Status}", oldStatus, invoice.Status);
            }
        }

        public async Task<decimal> CalculateInvoiceTotalAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var lines = await context.ExpenseInvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .ToListAsync();

            return lines.Sum(l => l.AmountInclVat);
        }

        // =====================================================
        // ANALYTICS
        // =====================================================

        public async Task<List<ExpenseInvoice>> GetCashFlowAsync(DateTime from, DateTime to)
        {
            using var context = _dbFactory.CreateDbContext();
            
            return await context.ExpenseInvoices
                .Where(i => i.DueDate >= from && i.DueDate <= to && i.Status != "PAID")
                .OrderBy(i => i.DueDate)
                .ToListAsync();
        }

        public async Task RecalculateInvoiceTotalsAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null)
                return;

            var lines = await context.ExpenseInvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .ToListAsync();

            invoice.AmountExclVat = lines.Sum(l => l.AmountExclVat);
            invoice.VatAmount = lines.Sum(l => l.AmountInclVat - l.AmountExclVat);
            invoice.AmountInclVat = lines.Sum(l => l.AmountInclVat);

            context.Entry(invoice).Property(i => i.AmountExclVat).IsModified = true;
            context.Entry(invoice).Property(i => i.VatAmount).IsModified = true;
            context.Entry(invoice).Property(i => i.AmountInclVat).IsModified = true;
            context.Entry(invoice).Property(i => i.UpdatedAt).IsModified = true;
            invoice.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync();
        }

        public async Task<List<ExpenseInvoice>> GetSupplierHistoryAsync(int supplierId, int year)
        {
            using var context = _dbFactory.CreateDbContext();
            
            return await context.ExpenseInvoices
                .Where(i => i.SupplierId == supplierId && i.InvoiceDate.Year == year)
                .OrderBy(i => i.InvoiceDate)
                .ToListAsync();
        }

        // =====================================================
        // AUDIT LOGGING
        // =====================================================

        private async Task LogAuditAsync(NordicBeesERPContext context, int invoiceId, 
            string invoiceNumber, string action, string? details = null,
            string? oldStatus = null, string? newStatus = null)
        {
            var audit = new NordicBeesERP.Models.Expenses.ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId,
                InvoiceNumber = invoiceNumber,
                Action = action,
                ActionDetails = details,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                PerformedAt = DateTime.UtcNow
            };
            await context.ExpenseInvoiceAudits.AddAsync(audit);
            await context.SaveChangesAsync();
        }

        // =====================================================
        // VALIDATION
        // =====================================================

        public async Task<int?> CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat, int excludeInvoiceId = 0)
        {
            if (string.IsNullOrEmpty(invoiceNumber)) return null;
            await using var ctx = _dbFactory.CreateDbContext();

            var query = ctx.ExpenseInvoices
                .Where(e =>
                    e.InvoiceNumber == invoiceNumber &&
                    e.InvoiceNumber != "" &&
                    e.Status != "REJECTED" &&
                    e.Status != "DUPLICATE_PENDING" &&
                    Math.Abs(e.AmountInclVat - amountInclVat) < 0.01m);

            // Exclude the current invoice from duplicate check (for retry scenarios)
            if (excludeInvoiceId > 0)
                query = query.Where(e => e.Id != excludeInvoiceId);

            var duplicate = await query
                .Select(e => e.Id)
                .FirstOrDefaultAsync();

            return duplicate > 0 ? duplicate : null;
        }

        // =====================================================
        // SUPPLIER ASSIGNMENT
        // =====================================================

        public async Task AssignSupplierAsync(int invoiceId, int supplierId, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null) return;
            var oldStatus = invoice.Status;
            invoice.SupplierId = supplierId;
            invoice.Status = "PENDING";
            invoice.UpdatedAt = DateTime.Now;

            var flags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags ?? "[]") ?? new();
            flags.Remove("VENDOR_NOT_FOUND");
            invoice.OcrFlags = System.Text.Json.JsonSerializer.Serialize(flags);

            context.Update(invoice);
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
                Action = "SUPPLIER_ASSIGNED", ActionDetails = $"Tiekėjo ID: {supplierId}",
                OldStatus = oldStatus, NewStatus = "PENDING",
                PerformedBy = performedBy, PerformedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        public async Task<int> AutoAssignSupplierAsync(string? vatCode, string? supplierName, int supplierId)
        {
            if (string.IsNullOrWhiteSpace(vatCode) && string.IsNullOrWhiteSpace(supplierName))
                return 0;

            using var context = _dbFactory.CreateDbContext();

            // Get supplier's default expense category
            var supplier = await context.BusinessPartners
                .Where(s => s.Id == supplierId)
                .Select(s => new { s.DefaultExpenseCategoryId })
                .FirstOrDefaultAsync();

            // Match by VAT code OR supplier name (OR logic)
            var matchingInvoices = await context.ExpenseInvoices
                .Where(i => i.Status == "PENDING_SUPPLIER"
                          && i.SupplierId != supplierId)
                .Where(i =>
                    (!string.IsNullOrWhiteSpace(vatCode)
                     && i.PendingSupplierVat != null
                     && i.PendingSupplierVat.Trim().ToUpper() == vatCode.Trim().ToUpper())
                    ||
                    (!string.IsNullOrWhiteSpace(supplierName)
                     && i.PendingSupplierName != null
                     && i.PendingSupplierName.Trim() == supplierName.Trim()))
                .ToListAsync();

            if (!matchingInvoices.Any())
                return 0;

            int assignedCount = 0;
            foreach (var invoice in matchingInvoices)
            {
                var oldStatus = invoice.Status;
                invoice.SupplierId = supplierId;
                invoice.Status = "PENDING";
                invoice.UpdatedAt = DateTime.Now;

                // Assign default category from supplier if invoice category is NULL
                if (supplier?.DefaultExpenseCategoryId.HasValue == true && invoice.CategoryId == null)
                {
                    invoice.CategoryId = supplier.DefaultExpenseCategoryId.Value;
                }

                var flags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags ?? "[]") ?? new();
                flags.Remove("VENDOR_NOT_FOUND");
                invoice.OcrFlags = System.Text.Json.JsonSerializer.Serialize(flags);

                context.Update(invoice);
                context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoice.InvoiceNumber,
                    Action = "SUPPLIER_AUTO_ASSIGNED",
                    ActionDetails = $"Auto-assign: VAT={vatCode}, Name={supplierName}",
                    OldStatus = oldStatus,
                    NewStatus = "PENDING",
                    PerformedBy = "SYSTEM",
                    PerformedAt = DateTime.Now
                });
                assignedCount++;
            }

            await context.SaveChangesAsync();
            return assignedCount;
        }

        public async Task ApproveAsync(int invoiceId, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null) return;
            var oldStatus = invoice.Status;
            invoice.Status = "PENDING";
            invoice.ApprovedBy = performedBy;
            invoice.ApprovedAt = DateTime.Now;
            invoice.UpdatedAt = DateTime.Now;
            context.Update(invoice);
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
                Action = "APPROVED", OldStatus = oldStatus, NewStatus = "PENDING",
                PerformedBy = performedBy, PerformedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        public async Task RestoreInvoiceAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_invoices SET status = 'NEEDS_REVIEW', rejected_reason = NULL, updated_at = {0} WHERE id = {1}",
                DateTime.Now, invoiceId);
        }

        public async Task RejectAsync(int invoiceId, string reason, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();
            var invoice = await context.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null) return;
            var oldStatus = invoice.Status;
            invoice.Status = "REJECTED";
            invoice.RejectedReason = reason;
            invoice.UpdatedAt = DateTime.Now;
            context.Update(invoice);
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoice.InvoiceNumber,
                Action = "REJECTED", ActionDetails = reason,
                OldStatus = oldStatus, NewStatus = "REJECTED",
                PerformedBy = performedBy, PerformedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
        }

        // =====================================================
        // OCR
        // =====================================================

        public async Task<ExpenseInvoice> CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL")
        {
            var currentUser = await _authService.GetAuthenticatedUserAsync();
            var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "system";

            string status;
            if (ocrResult.Flags.Contains(OcrFlag.WrongRecipient))
                status = "REJECTED";
            else if (ocrResult.SupplierId == null)
                status = "PENDING_SUPPLIER";
            else if (ocrResult.Flags.Any(f => f == OcrFlag.MissingAmount || f == OcrFlag.AmountMismatch ||
                                              f == OcrFlag.LowConfidence || f == OcrFlag.ZeroVat ||
                                              f == OcrFlag.MissingInvNumber))
                status = "NEEDS_REVIEW";
            else
                status = "PENDING";

            var duplicateId = await CheckDuplicateAsync(ocrResult.SupplierId, ocrResult.SupplierVatCode,
                ocrResult.InvoiceNumber, ocrResult.AmountInclVat);
            if (duplicateId.HasValue)
            {
                if (!ocrResult.Flags.Contains(OcrFlag.Duplicate)) ocrResult.Flags.Add(OcrFlag.Duplicate);
                status = "DUPLICATE_PENDING";
            }

            DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate);
            if (invoiceDate == default) invoiceDate = DateTime.Today;
            DateTime.TryParse(ocrResult.DueDate, out var dueDate);
            if (dueDate == default) dueDate = invoiceDate.AddDays(30);

            await using var ctx = _dbFactory.CreateDbContext();

            var invoice = new ExpenseInvoice
            {
                SupplierId = ocrResult.SupplierId,
                InvoiceType = "STANDARD",
                Source = source,
                OriginalFilePath = ocrResult.OriginalFilePath,
                OriginalFilename = ocrResult.OriginalFilename,
                PendingSupplierName = ocrResult.SupplierId == null ? ocrResult.SupplierName : null,
                PendingSupplierVat = ocrResult.SupplierId == null ? ocrResult.SupplierVatCode : null,
                PendingSupplierAddress = ocrResult.SupplierId == null ? ocrResult.SupplierAddress : null,
                PendingSupplierCity = ocrResult.SupplierId == null ? ocrResult.SupplierCity : null,
                PendingSupplierPostalCode = ocrResult.SupplierId == null ? ocrResult.SupplierPostalCode : null,
                PendingSupplierCountryCode = ocrResult.SupplierId == null ? ocrResult.SupplierCountryCode : null,
                PendingSupplierCompanyCode = ocrResult.SupplierId == null ? ocrResult.SupplierCompanyCode : null,
                PendingSupplierBankAccount = ocrResult.SupplierId == null ? ocrResult.SupplierBankAccount : null,
                InvoiceNumber = !string.IsNullOrWhiteSpace(ocrResult.InvoiceNumber) ? ocrResult.InvoiceNumber : null,
                InvoiceDate = invoiceDate,
                DueDate = dueDate,
                AmountExclVat = ocrResult.AmountExclVat,
                VatRate = ocrResult.VatRate,
                VatAmount = ocrResult.VatAmount,
                AmountInclVat = ocrResult.AmountInclVat,
                CategoryId = ocrResult.CategoryId,
                PaidAmount = 0,
                Currency = string.IsNullOrEmpty(ocrResult.Currency) ? "EUR" : ocrResult.Currency,
                Status = status,
                OcrStatus = "COMPLETED",
                OcrConfidence = ocrResult.Confidence.Overall,
                OcrPipeline = ocrResult.OcrPipeline,
                OcrFlags = ocrResult.Flags.Any() ? System.Text.Json.JsonSerializer.Serialize(ocrResult.Flags) : null,
                SupplierVatVerified = ocrResult.ViesVerified,
                SupplierVatVerifiedName = ocrResult.ViesName,
                RejectedReason = status == "REJECTED" ? "Sąskaita ne MB Lakštenai" : null,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            ctx.ExpenseInvoices.Add(invoice);
            await ctx.SaveChangesAsync();

            for (int i = 0; i < ocrResult.Lines.Count; i++)
            {
                var line = ocrResult.Lines[i];
                ctx.ExpenseInvoiceLines.Add(new ExpenseInvoiceLine
                {
                    InvoiceId = invoice.Id,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitOfMeasure = line.UnitOfMeasure,
                    AmountExclVat = line.AmountExclVat,
                    VatRate = line.VatRate,
                    AmountInclVat = line.AmountInclVat,
                    CategoryId = line.SuggestedCategoryId,
                    SortOrder = i + 1
                });
            }
            if (ocrResult.Lines.Any()) await ctx.SaveChangesAsync();

            ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Action = "CREATED",
                ActionDetails = $"Šaltinis: {source}, tikslumas: {ocrResult.Confidence.Overall}%, požymiai: {string.Join(", ", ocrResult.Flags)}",
                OldStatus = null,
                NewStatus = status,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            return invoice;
        }

        public async Task<ExpenseInvoice> UpdateFromOcrAsync(int invoiceId, OcrResultDto ocrResult)
        {
            var currentUser = await _authService.GetAuthenticatedUserAsync();
            var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "system";

            await using var ctx = _dbFactory.CreateDbContext();

            var invoice = await ctx.ExpenseInvoices.FindAsync(invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found");

            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;

            // Update invoice fields from OCR result
            DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate);
            if (invoiceDate == default) invoiceDate = DateTime.Today;
            DateTime.TryParse(ocrResult.DueDate, out var dueDate);
            if (dueDate == default) dueDate = invoiceDate.AddDays(30);

            invoice.SupplierId = ocrResult.SupplierId;
            invoice.PendingSupplierName = ocrResult.SupplierId == null ? ocrResult.SupplierName : null;
            invoice.PendingSupplierVat = ocrResult.SupplierId == null ? ocrResult.SupplierVatCode : null;
            invoice.PendingSupplierAddress = ocrResult.SupplierId == null ? ocrResult.SupplierAddress : null;
            invoice.PendingSupplierCity = ocrResult.SupplierId == null ? ocrResult.SupplierCity : null;
            invoice.PendingSupplierPostalCode = ocrResult.SupplierId == null ? ocrResult.SupplierPostalCode : null;
            invoice.PendingSupplierCountryCode = ocrResult.SupplierId == null ? ocrResult.SupplierCountryCode : null;
            invoice.PendingSupplierCompanyCode = ocrResult.SupplierId == null ? ocrResult.SupplierCompanyCode : null;
            invoice.PendingSupplierBankAccount = ocrResult.SupplierId == null ? ocrResult.SupplierBankAccount : null;
            invoice.InvoiceNumber = !string.IsNullOrWhiteSpace(ocrResult.InvoiceNumber) ? ocrResult.InvoiceNumber : null;
            invoice.InvoiceDate = invoiceDate;
            invoice.DueDate = dueDate;
            invoice.AmountExclVat = ocrResult.AmountExclVat;
            invoice.VatRate = ocrResult.VatRate;
            invoice.VatAmount = ocrResult.VatAmount;
            invoice.AmountInclVat = ocrResult.AmountInclVat;
            invoice.Currency = string.IsNullOrEmpty(ocrResult.Currency) ? "EUR" : ocrResult.Currency;
            invoice.OcrStatus = "COMPLETED";
            invoice.OcrConfidence = ocrResult.Confidence.Overall;
            invoice.OcrPipeline = ocrResult.OcrPipeline;
            if (!string.IsNullOrEmpty(ocrResult.OriginalFilePath))
                invoice.OriginalFilePath = ocrResult.OriginalFilePath;
            invoice.OriginalFilename = ocrResult.OriginalFilename;
            invoice.SupplierVatVerified = ocrResult.ViesVerified;
            invoice.SupplierVatVerifiedName = ocrResult.ViesName;
            invoice.UpdatedAt = DateTime.Now;

            // Determine flags and status
            var flags = new List<string>(ocrResult.Flags);
            var duplicateId = await CheckDuplicateAsync(ocrResult.SupplierId, ocrResult.SupplierVatCode,
                ocrResult.InvoiceNumber, ocrResult.AmountInclVat);
            if (duplicateId.HasValue && duplicateId.Value != invoiceId)
            {
                if (!flags.Contains(OcrFlag.Duplicate)) flags.Add(OcrFlag.Duplicate);
            }

            string newStatus;
            if (flags.Contains(OcrFlag.WrongRecipient))
                newStatus = "REJECTED";
            else if (ocrResult.SupplierId == null)
                newStatus = "PENDING_SUPPLIER";
            else if (flags.Any(f => f == OcrFlag.MissingAmount || f == OcrFlag.AmountMismatch ||
                                    f == OcrFlag.LowConfidence || f == OcrFlag.ZeroVat ||
                                    f == OcrFlag.MissingInvNumber))
                newStatus = "NEEDS_REVIEW";
            else
                newStatus = "PENDING";

            invoice.OcrFlags = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            invoice.Status = newStatus;
            invoice.RejectedReason = newStatus == "REJECTED" ? "Sąskaita ne MB Lakštenai" : null;

            // Mark all modified properties explicitly
            ctx.Entry(invoice).Property(i => i.SupplierId).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierName).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierVat).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierAddress).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierCity).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierPostalCode).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierCountryCode).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierCompanyCode).IsModified = true;
            ctx.Entry(invoice).Property(i => i.PendingSupplierBankAccount).IsModified = true;
            ctx.Entry(invoice).Property(i => i.InvoiceNumber).IsModified = true;
            ctx.Entry(invoice).Property(i => i.InvoiceDate).IsModified = true;
            ctx.Entry(invoice).Property(i => i.DueDate).IsModified = true;
            ctx.Entry(invoice).Property(i => i.AmountExclVat).IsModified = true;
            ctx.Entry(invoice).Property(i => i.VatRate).IsModified = true;
            ctx.Entry(invoice).Property(i => i.VatAmount).IsModified = true;
            ctx.Entry(invoice).Property(i => i.AmountInclVat).IsModified = true;
            ctx.Entry(invoice).Property(i => i.Currency).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OcrStatus).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OcrConfidence).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OcrPipeline).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OcrFlags).IsModified = true;
            ctx.Entry(invoice).Property(i => i.SupplierVatVerified).IsModified = true;
            ctx.Entry(invoice).Property(i => i.SupplierVatVerifiedName).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OriginalFilePath).IsModified = true;
            ctx.Entry(invoice).Property(i => i.OriginalFilename).IsModified = true;
            ctx.Entry(invoice).Property(i => i.Status).IsModified = true;
            ctx.Entry(invoice).Property(i => i.RejectedReason).IsModified = true;
            ctx.Entry(invoice).Property(i => i.UpdatedAt).IsModified = true;

            // Replace invoice lines
            var existingLines = await ctx.ExpenseInvoiceLines.Where(l => l.InvoiceId == invoiceId).ToListAsync();
            ctx.ExpenseInvoiceLines.RemoveRange(existingLines);

            for (int i = 0; i < ocrResult.Lines.Count; i++)
            {
                var line = ocrResult.Lines[i];
                ctx.ExpenseInvoiceLines.Add(new ExpenseInvoiceLine
                {
                    InvoiceId = invoice.Id,
                    Description = line.Description,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    UnitOfMeasure = line.UnitOfMeasure,
                    AmountExclVat = line.AmountExclVat,
                    VatRate = line.VatRate,
                    AmountInclVat = line.AmountInclVat,
                    CategoryId = line.SuggestedCategoryId,
                    SortOrder = i + 1
                });
            }
            await ctx.SaveChangesAsync();

            // Audit log
            ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = invoice.InvoiceNumber,
                Action = "OCR_RETRIED",
                ActionDetails = $"Pakartotinis OCR, tikslumas: {ocrResult.Confidence.Overall}%, požymiai: {string.Join(", ", flags)}",
                OldStatus = oldStatus,
                NewStatus = newStatus,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            return invoice;
        }
    }
}
