// =====================================================
// NORDIC BEES ERP - INVOICE SERVICE
// Framework: .NET 10
// Migrated from SaskaitosApp - Tested & Working Logic
// =====================================================

using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using System.Globalization;

namespace NordicBeesERP.Services
{
    public interface IInvoiceService
    {
        Task<Invoice?> GetInvoiceWithDetailsAsync(int id);
        Task<Invoice?> GetInvoiceAsync(int id);
        Task<int> CreateInvoiceAsync(Invoice invoice);
        Task<int> UpdateInvoiceAsync(Invoice invoice);
        Task<int> DeleteInvoiceAsync(int id);
        Task<int> UpdateInvoiceStatusAsync(int id, InvoiceStatus newStatus);
        Task<List<Customer>> GetCustomersAsync();
        Task<List<Product>> GetProductsAsync();
        Task<string> GenerateNextInvoiceNumberAsync(DateTime invoiceDate, string invoiceType);
        Invoice CalculateInvoiceTotals(Invoice invoice);
        Task<InvoiceStatistics> GetInvoiceStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);
        Task<List<Invoice>> GetInvoicesAsync(DateTime? fromDate = null, DateTime? toDate = null, InvoiceStatus? status = null, int? customerId = null, string? searchTerm = null, int take = 50, string? type = null);
        Task<int> CreateInvoiceFromDeliveryAsync(int deliveryId);
        Task<List<int>> GetInvoiceYearsAsync();
        Task<byte[]> GeneratePdfAsync(int invoiceId);
        Task<bool> IsInvoiceNumberTakenAsync(string invoiceNumber, int? excludeInvoiceId = null);
        Task<List<Invoice>> SearchInvoicesAsync(string searchTerm, int customerId);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly IAuthService _authService;

        public InvoiceService(IDbContextFactory<NordicBeesERPContext> contextFactory, IPdfGeneratorService pdfGeneratorService, IAuthService authService)
        {
            _contextFactory = contextFactory;
            _pdfGeneratorService = pdfGeneratorService;
            _authService = authService;
        }

        // =====================================================
        // INVOICE CRUD OPERATIONS
        // =====================================================

        public async Task<List<Invoice>> GetInvoicesAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            InvoiceStatus? status = null,
            int? customerId = null,
            string? searchTerm = null,
            int take = 50,
            string? type = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var query = context.Invoices
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(i => i.InvoiceDate >= fromDate!.Value);

            if (toDate.HasValue)
                query = query.Where(i => i.InvoiceDate <= toDate!.Value);

            if (status.HasValue)
                query = query.Where(i => i.Status == status.Value);

            if (customerId.HasValue)
                query = query.Where(i => i.CustomerId == customerId.Value);

            // Filter by invoice type based on prefix
            if (type == "sales")
                query = query.Where(i => i.InvoiceNumber.StartsWith("LAK"));
            else if (type == "purchase")
                query = query.Where(i => i.InvoiceNumber.StartsWith("ULAK"));

            // 1. Užkrauname visas sąskaitas be Customer (Include neleidžiamas)
            var invoices = await query.AsNoTracking().ToListAsync();

            // 2. Surandame tik tuos klijentus, kurie turi sąskaitas šiame sąraše
            var customerIds = invoices.Select(i => i.CustomerId).Distinct().ToList();
            var customers = await context.BusinessPartners
                .Where(bp => customerIds.Contains(bp.Id))
                .AsNoTracking()
                .ToListAsync();

            // 3. Priskiriame Customer kiekvienai sąskaitai
            foreach (var invoice in invoices)
            {
                invoice.Customer = customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            }

            // 4. Filtravimas pagal searchTerm (po duomenų užkrovimo)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                invoices = invoices.Where(i => 
                    i.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    (i.Customer != null && i.Customer.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            return invoices;
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var invoice = await context.Invoices
                .Include(i => i.Delivery)
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (invoice == null)
                return null;

            // Užkrauname klijentą atskirai - tik konkrečią sąskaitą
            var customers = await context.BusinessPartners
                .Where(bp => bp.Id == invoice.CustomerId)
                .AsNoTracking()
                .ToListAsync();
            
            invoice.Customer = customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            
            // Užkrauname InvoiceLines
            var lines = await context.InvoiceLines
                .Where(l => l.InvoiceId == id)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();
            
            invoice.Lines = lines;
            
            // Užkrauname products for each line
            var productIds = lines.Where(l => l.ProductId.HasValue).Select(l => l.ProductId.Value).Distinct().ToList();
            var products = await context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();
            
            foreach (var line in lines)
            {
                line.Product = products.FirstOrDefault(p => p.Id == line.ProductId);
            }
            
            return invoice;
        }

        public async Task<Invoice?> GetInvoiceAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var invoice = await context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (invoice == null)
                return null;

            // Užkrauname klijentą atskirai - tik konkrečią sąskaitą
            var customers = await context.BusinessPartners
                .Where(bp => bp.Id == invoice.CustomerId)
                .AsNoTracking()
                .ToListAsync();
            
            invoice.Customer = customers.FirstOrDefault(c => c.Id == invoice.CustomerId);
            
            return invoice;
        }

        public async Task<int> CreateInvoiceAsync(Invoice invoice)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Set timestamps
            invoice.CreatedAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            
            // Generate invoice number if not provided
            if (string.IsNullOrEmpty(invoice.InvoiceNumber))
            {
                invoice.InvoiceNumber = await GenerateNextInvoiceNumberAsync(invoice.InvoiceDate, invoice.InvoiceType);
            }

            // Calculate payment due date if not set
            if (!invoice.PaymentDueDate.HasValue && invoice.PaymentTermDays > 0)
            {
                invoice.PaymentDueDate = invoice.InvoiceDate.AddDays(invoice.PaymentTermDays);
            }

            // Calculate line numbers and totals
            int lineNumber = 1;
            foreach (var line in invoice.Lines)
            {
                line.LineNumber = lineNumber++;
                
                // Calculate line totals (SaskaitosApp logic)
                line.LineSubtotal = Math.Round(line.Quantity * line.PriceExclVat, 2);
                line.VatAmount = Math.Round(line.LineSubtotal * (line.VatRate / 100m), 2);
                line.LineTotal = Math.Round(line.LineSubtotal + line.VatAmount, 2);
            }

            // Calculate invoice totals
            invoice = CalculateInvoiceTotals(invoice);

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            
            if (invoice.DeliveryId.HasValue && invoice.DeliveryId > 0)
            {
                var delivery = await context.Deliveries.FindAsync(invoice.DeliveryId.Value);
                if (delivery != null)
                {
                    delivery.InvoiceId = invoice.Id;
                    delivery.InvoiceNumber = invoice.InvoiceNumber;
                    context.Entry(delivery).Property(d => d.InvoiceId).IsModified = true;
                    context.Entry(delivery).Property(d => d.InvoiceNumber).IsModified = true;
                    await context.SaveChangesAsync();
                }
            }
            
            return invoice.Id;
        }

        public async Task<int> UpdateInvoiceAsync(Invoice invoice)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            // Load existing invoice to preserve CreatedAt value
            var existingInvoice = await context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoice.Id);
            if (existingInvoice == null)
                throw new InvalidOperationException($"Sąskaita su id {invoice.Id} nerasta");
            
            // Preserve CreatedAt from original invoice
            invoice.CreatedAt = existingInvoice.CreatedAt;
            
            invoice.UpdatedAt = DateTime.UtcNow;

            // Calculate payment due date
            if (invoice.PaymentTermDays > 0)
            {
                invoice.PaymentDueDate = invoice.InvoiceDate.AddDays(invoice.PaymentTermDays);
            }

            // Recalculate line numbers and totals
            int lineNumber = 1;
            foreach (var line in invoice.Lines)
            {
                line.LineNumber = lineNumber++;
                
                // Recalculate line totals (SaskaitosApp logic)
                line.LineSubtotal = Math.Round(line.Quantity * line.PriceExclVat, 2);
                line.VatAmount = Math.Round(line.LineSubtotal * (line.VatRate / 100m), 2);
                line.LineTotal = Math.Round(line.LineSubtotal + line.VatAmount, 2);
            }

            // Recalculate invoice totals
            invoice = CalculateInvoiceTotals(invoice);

            // Get existing lines for this invoice
            var existingLines = await context.InvoiceLines
                .Where(l => l.InvoiceId == invoice.Id)
                .ToListAsync();

            // Get IDs of invoice lines that are referenced by credit notes
            var referencedLineIds = await context.CreditNoteLines
                .Where(cnl => cnl.InvoiceLineId.HasValue)
                .Select(cnl => cnl.InvoiceLineId.Value)
                .Distinct()
                .ToListAsync();

            // For lines referenced by credit notes, update in place using raw SQL
            // Lines NOT referenced can be deleted and recreated normally
            foreach (var existingLine in existingLines)
            {
                if (referencedLineIds.Contains(existingLine.Id))
                {
                    // Update this line in place to preserve FK integrity
                    // Lines referenced by credit notes are updated in place, not recreated, to preserve credit_note_lines.invoice_line_id FK integrity.
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE invoice_lines SET quantity = @p0, price_excl_vat = @p1, vat_rate = @p2, line_subtotal = @p3, vat_amount = @p4, line_total = @p5, description = @p6, product_id = @p7, updated_at = @p8 WHERE id = @p9",
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.Quantity ?? existingLine.Quantity,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.PriceExclVat ?? existingLine.PriceExclVat,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.VatRate ?? existingLine.VatRate,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.LineSubtotal ?? existingLine.LineSubtotal,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.VatAmount ?? existingLine.VatAmount,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.LineTotal ?? existingLine.LineTotal,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.Description ?? existingLine.Description,
                        invoice.Lines.FirstOrDefault(l => l.LineNumber == existingLine.LineNumber)?.ProductId,
                        DateTime.UtcNow,
                        existingLine.Id
                    );
                }
            }

            // Delete lines that are NOT referenced by credit notes (not updated in place above)
            var linesToUpdate = existingLines.Where(l => referencedLineIds.Contains(l.Id)).Select(l => l.Id).ToHashSet();
            var linesToRemove = existingLines.Where(l => !linesToUpdate.Contains(l.Id)).ToList();
            if (linesToRemove.Any())
            {
                context.InvoiceLines.RemoveRange(linesToRemove);
            }

            // Update invoice
            context.Invoices.Update(invoice);
            
            await context.SaveChangesAsync();
            return invoice.Id;
        }

        public async Task<int> DeleteInvoiceAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var invoice = await context.Invoices.FindAsync(id);
            if (invoice == null)
                return 0;

            context.Invoices.Remove(invoice);
            return await context.SaveChangesAsync();
        }

        public async Task<int> UpdateInvoiceStatusAsync(int id, InvoiceStatus newStatus)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var invoice = await context.Invoices.FindAsync(id);
            if (invoice == null)
                return 0;

            context.Attach(invoice);

            // Validate: block confirmation when total is zero but lines exist
            if (newStatus == InvoiceStatus.Confirmed &&
                invoice.TotalInclVat == 0 &&
                invoice.Lines != null &&
                invoice.Lines.Count > 0)
            {
                throw new InvalidOperationException(
                    "Invoice has lines but total is zero. Please check invoice lines before confirming.");
            }

            // Calculate payment due date when confirming if not set
            if (newStatus == InvoiceStatus.Confirmed &&
                !invoice.PaymentDueDate.HasValue &&
                invoice.PaymentTermDays > 0)
            {
                invoice.PaymentDueDate = invoice.InvoiceDate.AddDays(invoice.PaymentTermDays);
            }

            var oldStatus = invoice.Status;
            invoice.Status = newStatus;
            invoice.UpdatedAt = DateTime.UtcNow;

            // Save status change first
            await context.SaveChangesAsync();

            // Insert audit log entry if status actually changed
            if (oldStatus != newStatus)
            {
                var currentUser = await _authService.GetAuthenticatedUserAsync();
                var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "system";

                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO invoice_audit (invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                    invoice.Id,
                    invoice.InvoiceNumber,
                    "StatusChange",
                    $"Statusas pakeistas iš {oldStatus} į {newStatus}",
                    oldStatus.ToString(),
                    newStatus.ToString(),
                    performedBy,
                    DateTime.UtcNow
                );
            }

            return invoice.Id;
        }

        // =====================================================
        // CALCULATION HELPERS (from SaskaitosApp)
        // =====================================================

        public Invoice CalculateInvoiceTotals(Invoice invoice)
        {
            decimal subtotalExclVat = 0;
            decimal totalVat = 0;
            decimal totalInclVat = 0;

            foreach (var line in invoice.Lines)
            {
                subtotalExclVat += line.LineSubtotal;
                totalVat += line.VatAmount;
                totalInclVat += line.LineTotal;
            }

            invoice.SubtotalExclVat = Math.Round(subtotalExclVat, 2);
            invoice.TotalVat = Math.Round(totalVat, 2);
            invoice.TotalInclVat = Math.Round(totalInclVat, 2);

            return invoice;
        }

        // =====================================================
        // INVOICE NUMBER GENERATION (from SaskaitosApp)
        // =====================================================

        public async Task<string> GenerateNextInvoiceNumberAsync(DateTime invoiceDate, string invoiceType)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var year = invoiceDate.Year;
            var yearSuffix = (year % 100).ToString("D2"); // Last 2 digits of year

            // Check if this is a 6% purchase invoice (ULAK series)
            bool isPurchaseInvoice = invoiceType.Contains("6%");
            
            string prefix = isPurchaseInvoice ? "ULAK" : "LAK";
            string searchPrefix = prefix + yearSuffix;

            // Use raw SQL to completely bypass EF schema issues
            var lastNumber = await context.Invoices
                .Where(i => i.InvoiceNumber.StartsWith(searchPrefix))
                .OrderByDescending(i => i.InvoiceNumber)
                .Select(i => i.InvoiceNumber)
                .FirstOrDefaultAsync();

            int nextNumber = 1;
            if (!string.IsNullOrEmpty(lastNumber))
            {
                // Extract number from prefix + YY + 000 format
                var numPart = lastNumber.Substring(searchPrefix.Length);
                if (int.TryParse(numPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            return $"{prefix}{yearSuffix}{nextNumber:D3}";
        }

        public async Task<bool> IsInvoiceNumberTakenAsync(string invoiceNumber, int? excludeInvoiceId = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.Invoices.AsNoTracking().AnyAsync(i =>
                i.InvoiceNumber == invoiceNumber &&
                (excludeInvoiceId == null || i.Id != excludeInvoiceId));
        }

        // =====================================================
        // REFERENCE DATA METHODS
        // =====================================================

        public async Task<List<Customer>> GetCustomersAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            return await context.BusinessPartners
                .Where(bp => bp.IsActive &&
                             (bp.PartnerType == PartnerType.Customer ||
                              bp.PartnerType == PartnerType.Both))
                .GroupJoin(
                    context.Invoices,
                    bp => bp.Id,
                    i => i.CustomerId,
                    (bp, invoices) => new { Partner = bp, InvoiceCount = invoices.Count() })
                .OrderByDescending(x => x.InvoiceCount)
                .ThenBy(x => x.Partner.Name)
                .Select(x => new Customer
                {
                    Id = x.Partner.Id,
                    Name = x.Partner.Name,
                    VatCode = x.Partner.VatCode,
                    PaymentTermDays = x.Partner.PaymentTermDays,
                    DefaultLanguage = x.Partner.DefaultLanguage,
                    DefaultVatRate = x.Partner.DefaultVatRate
                })
                .ToListAsync();
        }


        public async Task<List<Product>> GetProductsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Code)
                .ToListAsync();
        }
        // =====================================================
        // INVOICE STATISTICS
        // =====================================================

        public async Task<InvoiceStatistics> GetInvoiceStatisticsAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var query = context.Invoices.AsQueryable();

            if (fromDate.HasValue)
                query = query.Where(i => i.InvoiceDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(i => i.InvoiceDate <= toDate.Value);

            // Filter to include only sales invoices (LAK prefix)
            query = query.Where(i => i.InvoiceNumber.StartsWith("LAK"));

            var stats = new InvoiceStatistics
            {
                TotalCount = await query.CountAsync(),
                DraftCount = await query.Where(i => i.Status == InvoiceStatus.Draft).CountAsync(),
                ConfirmedCount = await query.Where(i => i.Status == InvoiceStatus.Confirmed).CountAsync(),
                PaidCount = await query.Where(i => i.Status == InvoiceStatus.Paid).CountAsync(),
                DisputedCount = await query.Where(i => i.Status == InvoiceStatus.Disputed).CountAsync(),

                TotalAmountExclVat = await query.SumAsync(i => (decimal?)i.SubtotalExclVat) ?? 0m,
                TotalVatAmount = await query.SumAsync(i => (decimal?)i.TotalVat) ?? 0m,
                TotalAmountInclVat = await query.SumAsync(i => (decimal?)i.TotalInclVat) ?? 0m,

                AverageInvoiceAmount = await query.AverageAsync(i => (decimal?)i.TotalInclVat) ?? 0m,
                LargestInvoice = await query.OrderByDescending(i => i.TotalInclVat).FirstOrDefaultAsync()
            };

            // Unpaid = Confirmed invoices (not yet paid)
            stats.UnpaidAmount = await query
                .Where(i => i.Status == InvoiceStatus.Confirmed)
                .SumAsync(i => (decimal?)i.TotalInclVat) ?? 0m;

            return stats;
        }

        public async Task<List<int>> GetInvoiceYearsAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.Invoices
                .Select(i => i.InvoiceDate.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToListAsync();
        }

        public async Task<byte[]> GeneratePdfAsync(int invoiceId)
        {
            return await _pdfGeneratorService.GenerateInvoicePdfAsync(invoiceId);
        }

        // =====================================================
        // SEARCH METHODS (for autocomplete)
        // =====================================================

        public async Task<List<Invoice>> SearchInvoicesAsync(string searchTerm, int customerId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var query = context.Invoices
                .Include(i => i.Customer)
                .Where(i => i.CustomerId == customerId && i.Status != InvoiceStatus.Cancelled);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i => 
                    i.InvoiceNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.Customer.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            return await query
                .OrderByDescending(i => i.InvoiceDate)
                .ThenByDescending(i => i.Id)
                .Take(10)
                .ToListAsync();
        }

        // =====================================================
        // USER/CONTEXT METHODS
        // =====================================================

        public async Task<int?> GetCustomerIdAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var authState = await context.ErpUsers
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, u.Email })
                .FirstOrDefaultAsync();
            
            if (authState == null) return null;
            
            // Get customer associated with this ERP user
            return await context.BusinessPartners
                .Where(bp => bp.PartnerType == PartnerType.Customer)
                .Select(bp => bp.Id)
                .FirstOrDefaultAsync();
        }

        public async Task<int?> GetUserIdAsync()
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var authState = await context.ErpUsers
                .Where(u => u.IsActive)
                
                .Select(u => u.Id)
                .FirstOrDefaultAsync();
            
            return authState;
        }

        public async Task<int> CreateInvoiceFromDeliveryAsync(int deliveryId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var delivery = await context.Deliveries
                .Include(d => d.RawMaterialType)
                .FirstOrDefaultAsync(d => d.Id == deliveryId);
            
            if (delivery == null)
                throw new InvalidOperationException($"Pristatymas su id {deliveryId} nerastas");
            
            // Get supplier info
            var supplier = await context.BusinessPartners.FindAsync(delivery.SupplierId);
            if (supplier == null)
                throw new InvalidOperationException($"Tiekėjas su id {delivery.SupplierId} nerastas");
            
            // Generate invoice number
            var invoiceNumber = await GenerateNextInvoiceNumberAsync(delivery.DeliveryDate, "6% PVM SĄSKAITA FAKTŪRA");
            
            // Create invoice
            var invoice = new Invoice
            {
                InvoiceNumber = invoiceNumber,
                InvoiceDate = delivery.DeliveryDate,
                CustomerId = delivery.SupplierId,
                DeliveryId = delivery.Id,
                PaymentTermDays = supplier.PaymentTermDays,
                PaymentDueDate = delivery.DeliveryDate.AddDays(supplier.PaymentTermDays),
                Language = "LT",
                InvoiceType = "6% PVM SĄSKAITA FAKTŪRA",
                Status = InvoiceStatus.Draft,
                Lines = new List<InvoiceLine>()
            };
            
            // Add invoice line for the delivery
            int lineNumber = 1;
            invoice.Lines.Add(new InvoiceLine
            {
                LineNumber = lineNumber++,
                Description = $"{delivery.RawMaterialType?.Name ?? "Žaliava"} - {delivery.DeliveryNumber}",
                Quantity = delivery.TotalNetWeight,
                Unit = "kg",
                PriceExclVat = delivery.TotalNetWeight > 0 ? (delivery.TotalAmount / delivery.TotalNetWeight) : 0,
                VatRate = 6
            });
            
            // Calculate totals
            invoice = CalculateInvoiceTotals(invoice);
            
            context.Invoices.Add(invoice);
            
            // Update delivery with invoice reference
            delivery.InvoiceId = invoice.Id;
            delivery.InvoiceNumber = invoiceNumber;
            context.Entry(delivery).Property(d => d.InvoiceId).IsModified = true;
            context.Entry(delivery).Property(d => d.InvoiceNumber).IsModified = true;
            
            await context.SaveChangesAsync();
            
            return invoice.Id;
        }
    }

    public class InvoiceStatistics
    {
        public int TotalCount { get; set; }
        public int DraftCount { get; set; }
        public int ConfirmedCount { get; set; }
        public int PaidCount { get; set; }
        public int DisputedCount { get; set; }

        public decimal TotalAmountExclVat { get; set; }
        public decimal TotalVatAmount { get; set; }
        public decimal TotalAmountInclVat { get; set; }
        public decimal UnpaidAmount { get; set; }

        public decimal AverageInvoiceAmount { get; set; }
        public Invoice? LargestInvoice { get; set; }
    }
}