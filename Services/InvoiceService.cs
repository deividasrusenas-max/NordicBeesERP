// =====================================================
// NORDIC BEES ERP - INVOICE SERVICE
// Framework: .NET 10
// Migrated from SaskaitosApp - Tested & Working Logic
// =====================================================

using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
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
        Task<int> CreateInvoiceFromDeliveryAsync(int deliveryId, decimal transportCost, decimal barrelCost, decimal otherCost, int? recipientSupplierId = null);
        Task<List<int>> GetInvoiceYearsAsync();
        Task<byte[]> GeneratePdfAsync(int invoiceId);

        /// <summary>
        /// Returns invoice PDF bytes. For a final (non-Draft) invoice whose
        /// PDF was already generated and cached (pdf_path set AND file present),
        /// serves the cached byte-identical copy. Otherwise generates the PDF
        /// live; and if the invoice is final, ALSO saves it to disk and records
        /// pdf_path — freezing the content (incl. partner address) as of first
        /// generation so reprints never change. Draft invoices always generate
        /// live and are never saved.
        /// </summary>
        Task<byte[]> GenerateAndSavePdfAsync(int invoiceId);

        /// <summary>
        /// Persists already-generated PDF bytes for a final invoice (used right
        /// after the Draft→Confirmed transition when the confirm step already
        /// produced the PDF): saves to disk and records pdf_path. No-op for
        /// Draft invoices / null or empty bytes / missing invoice.
        /// </summary>
        Task SaveFinalizedPdfAsync(int invoiceId, byte[] pdfBytes);

        Task<bool> IsInvoiceNumberTakenAsync(string invoiceNumber, int? excludeInvoiceId = null);
        Task<List<Invoice>> SearchInvoicesAsync(string searchTerm, int customerId);

        /// <summary>
        /// Monthly sold volume (netto kg) for sales invoices (LAK prefix, "kg" unit lines),
        /// excluding Draft/Cancelled, grouped by year+month across the given year range.
        /// Used by the Home.razor "Pardavimų apimtis" dashboard trend chart.
        /// </summary>
        Task<List<MonthlyVolumePoint>> GetMonthlySalesVolumeAsync(int fromYear, int toYear);
    }

    public class InvoiceService : IInvoiceService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
        private readonly IPdfGeneratorService _pdfGeneratorService;
        private readonly IAuthService _authService;
        private readonly string _pdfBaseDir;

        public InvoiceService(IDbContextFactory<NordicBeesERPContext> contextFactory, IPdfGeneratorService pdfGeneratorService, IAuthService authService, string? pdfBaseDir = null)
        {
            _contextFactory = contextFactory;
            _pdfGeneratorService = pdfGeneratorService;
            _authService = authService;
            _pdfBaseDir = pdfBaseDir ?? "/var/lib/nordicbees/invoices";
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

            bool isRc96 = invoice.InvoiceType == InvoiceTypes.ReverseCharge96;
            invoice.ReverseCharge = isRc96;

            // Calculate line numbers and totals
            int lineNumber = 1;
            foreach (var line in invoice.Lines)
            {
                line.LineNumber = lineNumber++;
                

                // Calculate line totals (SaskaitosApp logic)
                line.LineSubtotal = Math.Round(line.Quantity * line.PriceExclVat, 2, MidpointRounding.AwayFromZero);
                line.VatAmount = isRc96 ? 0m : Math.Round(line.LineSubtotal * (line.VatRate / 100m), 2, MidpointRounding.AwayFromZero);
                line.LineTotal = Math.Round(line.LineSubtotal + line.VatAmount, 2, MidpointRounding.AwayFromZero);
            }

            // Calculate invoice totals
            invoice = CalculateInvoiceTotals(invoice);

            // Snapshot the customer's VAT code at issuance (forward-looking; never changes retroactively)
            var customerPartner = await context.BusinessPartners.AsNoTracking()
                .FirstOrDefaultAsync(bp => bp.Id == invoice.CustomerId);
            invoice.CustomerVatCode = customerPartner?.VatCode;

            context.Invoices.Add(invoice);
            await context.SaveChangesAsync();
            
            if (invoice.DeliveryId.HasValue && invoice.DeliveryId > 0)
            {
                var deliveryExists = await context.Deliveries.AsNoTracking().AnyAsync(d => d.Id == invoice.DeliveryId.Value);
                if (deliveryExists)
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE deliveries SET invoice_id = {0}, invoice_number = {1}, updated_at = NOW() WHERE id = {2}",
                        invoice.Id, invoice.InvoiceNumber, invoice.DeliveryId.Value);
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

            bool isRc96 = invoice.InvoiceType == InvoiceTypes.ReverseCharge96;
            invoice.ReverseCharge = isRc96;

            // Recalculate line numbers and totals
            int lineNumber = 1;
            foreach (var line in invoice.Lines)
            {
                line.LineNumber = lineNumber++;
                

                // Recalculate line totals (SaskaitosApp logic)
                line.LineSubtotal = Math.Round(line.Quantity * line.PriceExclVat, 2, MidpointRounding.AwayFromZero);
                line.VatAmount = isRc96 ? 0m : Math.Round(line.LineSubtotal * (line.VatRate / 100m), 2, MidpointRounding.AwayFromZero);
                line.LineTotal = Math.Round(line.LineSubtotal + line.VatAmount, 2, MidpointRounding.AwayFromZero);
            }

            // Recalculate invoice totals
            invoice = CalculateInvoiceTotals(invoice);

            var incoming = invoice.Lines.ToList();

            if (incoming.Where(l => l.Id > 0).GroupBy(l => l.Id).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Sąskaitos eilutės kartojasi (pasikartojantis eilutės Id).");

            await using var transaction = await context.Database.BeginTransactionAsync();

            var existing = await context.InvoiceLines
                .AsNoTracking()
                .Where(l => l.InvoiceId == invoice.Id)
                .ToListAsync();
            var existingIds = existing.Select(l => l.Id).ToHashSet();

            // Lines with an Id that does not belong to THIS invoice are rejected
            if (incoming.Any(l => l.Id > 0 && !existingIds.Contains(l.Id)))
                throw new InvalidOperationException("Eilutė nepriklauso šiai sąskaitai.");

            // Only credit-note links of THIS invoice's lines matter
            var existingIdList = existingIds.ToList();
            var referencedIds = (await context.CreditNoteLines
                .Where(cnl => cnl.InvoiceLineId.HasValue && existingIdList.Contains(cnl.InvoiceLineId.Value))
                .Select(cnl => cnl.InvoiceLineId!.Value)
                .Distinct()
                .ToListAsync()).ToHashSet();

            var incomingById = incoming.Where(l => l.Id > 0).ToDictionary(l => l.Id);

            foreach (var ex in existing)
            {
                if (incomingById.TryGetValue(ex.Id, out var line))
                {
                    // Update in place by Id — the line keeps its Id, so credit_note_lines.invoice_line_id stays valid
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE invoice_lines SET line_number = {0}, product_id = {1}, product_code = {2}, description = {3}, quantity = {4}, unit = {5}, price_excl_vat = {6}, vat_rate = {7}, line_subtotal = {8}, vat_amount = {9}, line_total = {10}, updated_at = {11} WHERE id = {12} AND invoice_id = {13}",
                        line.LineNumber, line.ProductId, line.ProductCode, line.Description ?? "", line.Quantity, line.Unit,
                        line.PriceExclVat, line.VatRate, line.LineSubtotal, line.VatAmount, line.LineTotal,
                        DateTime.UtcNow, ex.Id, invoice.Id);
                }
                else if (referencedIds.Contains(ex.Id))
                {
                    throw new InvalidOperationException(
                        $"Eilutė '{ex.Description}' susieta su kreditine sąskaita — jos ištrinti negalima.");
                }
                else
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "DELETE FROM invoice_lines WHERE id = {0} AND invoice_id = {1}", ex.Id, invoice.Id);
                }
            }

            foreach (var line in incoming.Where(l => l.Id == 0))
            {
                var now = DateTime.UtcNow;
                await context.Database.ExecuteSqlRawAsync(
                    "INSERT INTO invoice_lines (invoice_id, line_number, product_id, product_code, lot_number, description, quantity, unit, price_excl_vat, vat_rate, line_subtotal, vat_amount, line_total, notes, created_at, updated_at) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15})",
                    invoice.Id, line.LineNumber, line.ProductId, line.ProductCode, line.LotNumber, line.Description ?? "",
                    line.Quantity, line.Unit, line.PriceExclVat, line.VatRate, line.LineSubtotal, line.VatAmount,
                    line.LineTotal, line.Notes, now, now);
            }

            // Header: update ONLY the user-editable columns. paid_amount, payment_status, last_payment_date, pdf_path,
            // delivery_id, currency_id, due_date, created_at, status and invoice_number are deliberately left untouched.
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE invoices SET invoice_date = {0}, customer_id = {1}, payment_term_days = {2}, payment_due_date = {3}, language = {4}, invoice_type = {5}, reverse_charge = {6}, subtotal_excl_vat = {7}, total_vat = {8}, total_incl_vat = {9}, notes = {10}, updated_at = {11} WHERE id = {12}",
                invoice.InvoiceDate, invoice.CustomerId, invoice.PaymentTermDays, invoice.PaymentDueDate, invoice.Language,
                invoice.InvoiceType, invoice.ReverseCharge, invoice.SubtotalExclVat, invoice.TotalVat, invoice.TotalInclVat,
                invoice.Notes, invoice.UpdatedAt, invoice.Id);

            // The VAT-code snapshot is re-taken only when the customer actually changed
            if (invoice.CustomerId != existingInvoice.CustomerId)
            {
                var vatCode = await context.BusinessPartners.AsNoTracking()
                    .Where(bp => bp.Id == invoice.CustomerId)
                    .Select(bp => bp.VatCode)
                    .FirstOrDefaultAsync();
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE invoices SET customer_vat_code = {0} WHERE id = {1}", vatCode, invoice.Id);
            }

            await transaction.CommitAsync();
            return invoice.Id;
        }

        public async Task<int> DeleteInvoiceAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", id);
            return 1;
        }

        public async Task<int> UpdateInvoiceStatusAsync(int id, InvoiceStatus newStatus)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var invoice = await context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id);
            if (invoice == null)
                return 0;

            // Validate: block confirmation when total is zero but lines exist
            int lineCount = await context.InvoiceLines.CountAsync(l => l.InvoiceId == id);
            if (newStatus == InvoiceStatus.Confirmed &&
                invoice.TotalInclVat == 0 &&
                lineCount > 0)
            {
                throw new InvalidOperationException(
                    "Sąskaita turi eilučių, bet bendra suma lygi nuliui. Patikrinkite sąskaitos eilutes prieš tvirtindami.");
            }

            // Calculate payment due date when confirming if not set
            if (newStatus == InvoiceStatus.Confirmed &&
                !invoice.PaymentDueDate.HasValue &&
                invoice.PaymentTermDays > 0)
            {
                invoice.PaymentDueDate = invoice.InvoiceDate.AddDays(invoice.PaymentTermDays);
            }

            var oldStatus = invoice.Status;

            // Save status change via raw SQL (NoTracking — Update + SaveChanges would silently do nothing)
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE invoices SET status = {0}, updated_at = {1}, payment_due_date = {2} WHERE id = {3}",
                newStatus.ToString(),
                DateTime.UtcNow,
                invoice.PaymentDueDate,
                id
            );

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
                             (bp.IsCustomer
                              || (bp.IsCustomer == false && bp.IsSupplier == false && bp.IsExpenseSupplier == false
                                  && (bp.PartnerType == PartnerType.Customer
                                      || bp.PartnerType == PartnerType.Both))))
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

        public async Task<List<MonthlyVolumePoint>> GetMonthlySalesVolumeAsync(int fromYear, int toYear)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var query = from il in context.InvoiceLines
                        join inv in context.Invoices on il.InvoiceId equals inv.Id
                        where inv.InvoiceNumber.StartsWith("LAK")
                              && il.Unit == "kg"
                              && inv.InvoiceDate.Year >= fromYear && inv.InvoiceDate.Year <= toYear
                              && inv.Status != InvoiceStatus.Draft && inv.Status != InvoiceStatus.Cancelled
                        group il.Quantity by new { inv.InvoiceDate.Year, inv.InvoiceDate.Month } into g
                        select new MonthlyVolumePoint { Year = g.Key.Year, Month = g.Key.Month, NetKg = g.Sum() };

            return await query.ToListAsync();
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

        public async Task<byte[]> GenerateAndSavePdfAsync(int invoiceId)
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Sąskaita {invoiceId} nerasta");

            var isFinal = invoice.Status != InvoiceStatus.Draft;

            // Serve cached copy when already frozen
            if (isFinal && !string.IsNullOrWhiteSpace(invoice.PdfPath))
            {
                var cached = PdfFileCache.TryRead(_pdfBaseDir, invoice.PdfPath);
                if (cached != null)
                    return cached;
            }

            var pdfBytes = await _pdfGeneratorService.GenerateInvoicePdfAsync(invoiceId);

            // Freeze only final invoices
            if (isFinal)
            {
                var relativePath = PdfFileCache.Save(
                    _pdfBaseDir, invoice.InvoiceDate.Year, $"{invoice.InvoiceNumber}.pdf", pdfBytes);
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE invoices SET pdf_path = {0}, updated_at = {1} WHERE id = {2}",
                    relativePath, DateTime.UtcNow, invoiceId);
            }

            return pdfBytes;
        }

        public async Task SaveFinalizedPdfAsync(int invoiceId, byte[] pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0) return;

            await using var context = await _contextFactory.CreateDbContextAsync();
            var invoice = await context.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null || invoice.Status == InvoiceStatus.Draft) return;

            var relativePath = PdfFileCache.Save(
                _pdfBaseDir, invoice.InvoiceDate.Year, $"{invoice.InvoiceNumber}.pdf", pdfBytes);
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE invoices SET pdf_path = {0}, updated_at = {1} WHERE id = {2}",
                relativePath, DateTime.UtcNow, invoiceId);
        }

        // =====================================================
        // SEARCH METHODS (for autocomplete)
        // =====================================================

        public async Task<List<Invoice>> SearchInvoicesAsync(string searchTerm, int customerId)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            
            var query = context.Invoices
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId && i.Status != InvoiceStatus.Cancelled);

            if (!string.IsNullOrEmpty(searchTerm))
            {
                var pattern = $"%{searchTerm}%";
                query = query.Where(i => 
                    EF.Functions.Like(i.InvoiceNumber, pattern) ||
                    EF.Functions.Like(i.Customer.Name, pattern));
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
                .Where(bp => bp.IsCustomer
                             || (bp.IsCustomer == false && bp.IsSupplier == false && bp.IsExpenseSupplier == false
                                 && bp.PartnerType == PartnerType.Customer))
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
            return await CreateInvoiceFromDeliveryAsync(deliveryId, 0m, 0m, 0m, null);
        }

        public async Task<int> CreateInvoiceFromDeliveryAsync(int deliveryId, decimal transportCost, decimal barrelCost, decimal otherCost, int? recipientSupplierId = null)
        {
            using var context = await _contextFactory.CreateDbContextAsync();

            var delivery = await context.Deliveries
                .Include(d => d.RawMaterialType)
                .FirstOrDefaultAsync(d => d.Id == deliveryId);

            if (delivery == null)
                throw new InvalidOperationException($"Pristatymas su id {deliveryId} nerastas");

            // Resolve the invoice recipient (may differ from the delivery's supplier)
            var invoiceSupplierId = recipientSupplierId ?? delivery.SupplierId;
            var supplier = await context.BusinessPartners.AsNoTracking().FirstOrDefaultAsync(bp => bp.Id == invoiceSupplierId);
            if (supplier == null)
                throw new InvalidOperationException($"Tiekėjas su id {invoiceSupplierId} nerastas");

            var missingFarmerFields = SupplierFarmerHelper.GetMissingOrInvalidFields(supplier);
            if (missingFarmerFields.Count > 0)
                throw new InvalidOperationException(
                    $"Negalima išrašyti sąskaitos — ūkininko duomenys neišsamūs: {string.Join("; ", missingFarmerFields)}");

            var deductions = transportCost + barrelCost + otherCost;
            var unitPrice = delivery.TotalNetWeight > 0
                ? (delivery.TotalAmount - deductions) / delivery.TotalNetWeight
                : 0m;

            // Build the deduction summary for internal record only — not written to Invoice.Notes
            // (deductions remain persisted on the delivery via the ExecuteSqlRawAsync call below;
            // the invoice itself should not display them as a comment).

            var paymentTermDays = supplier.PaymentTermDays > 0 ? supplier.PaymentTermDays : 10;

            var invoice = new Invoice
            {
                // InvoiceNumber intentionally left empty — CreateInvoiceAsync generates it.
                InvoiceDate = delivery.DeliveryDate,
                CustomerId = invoiceSupplierId,
                DeliveryId = delivery.Id,
                PaymentTermDays = paymentTermDays,
                PaymentDueDate = delivery.DeliveryDate.AddDays(paymentTermDays),
                Language = "LT",
                InvoiceType = "6% PVM SĄSKAITA FAKTŪRA",
                Status = InvoiceStatus.Draft,
                Notes = null,
                Lines = new List<InvoiceLine>
                {
                    new InvoiceLine
                    {
                        LineNumber = 1,
                        Description = delivery.RawMaterialType?.Name ?? "Žaliava",
                        Quantity = delivery.TotalNetWeight,
                        Unit = "kg",
                        PriceExclVat = unitPrice,
                        VatRate = 6
                    }
                }
            };

            // CreateInvoiceAsync creates the invoice + line, computes totals, snapshots the
            // supplier VAT code, and updates deliveries.invoice_id/invoice_number linkage.
            var invoiceId = await CreateInvoiceAsync(invoice);

            // Persist the three deduction amounts on the delivery (delivery is detached — raw SQL).
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE deliveries SET transport_cost_deduction = {0}, barrel_cost_deduction = {1}, other_cost_deduction = {2}, updated_at = NOW() WHERE id = {3}",
                transportCost, barrelCost, otherCost, deliveryId);

            return invoiceId;
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

    public class MonthlyVolumePoint
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal NetKg { get; set; }
    }
}