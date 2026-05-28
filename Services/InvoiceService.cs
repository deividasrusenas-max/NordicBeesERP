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
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
        private readonly IPdfGeneratorService _pdfGeneratorService;

        public InvoiceService(IDbContextFactory<NordicBeesERPContext> contextFactory, IPdfGeneratorService pdfGeneratorService)
        {
            _contextFactory = contextFactory;
            _pdfGeneratorService = pdfGeneratorService;
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
            var invoices = await query.ToListAsync();

            // 2. Surandame visus klijentus (BusinessPartners su Customer type)
            var customers = await context.BusinessPartners
                .Where(bp => true)
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
                    i.InvoiceNumber.Contains(searchTerm) ||
                    (i.Customer != null && i.Customer.Name.Contains(searchTerm))
                ).ToList();
            }

            return invoices;
        }

        public async Task<Invoice?> GetInvoiceWithDetailsAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var invoice = await context.Invoices
                .Include(i => i.Delivery)
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (invoice == null)
                return null;

            // Užkrauname klijentą atskirai (be Include)
            var customers = await context.BusinessPartners
                .Where(bp => true)
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
                .FirstOrDefaultAsync(i => i.Id == id);
            
            if (invoice == null)
                return null;

            // Užkrauname klijentą atskirai (be Include)
            var customers = await context.BusinessPartners
                .Where(bp => true)
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

            // Remove existing lines
            var existingLines = await context.InvoiceLines
                .Where(l => l.InvoiceId == invoice.Id)
                .ToListAsync();
            context.InvoiceLines.RemoveRange(existingLines);

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

            invoice.Status = newStatus;
            invoice.UpdatedAt = DateTime.UtcNow;
            return await context.SaveChangesAsync();
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
                .OrderBy(bp => bp.Name)
                .Select(bp => new Customer { Id = bp.Id, Name = bp.Name, VatCode = bp.VatCode })
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
                    i.InvoiceNumber.Contains(searchTerm) || 
                    i.Customer.Name.Contains(searchTerm));
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