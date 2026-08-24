// =====================================================
// NORDIC BEES ERP - CREDIT NOTE SERVICE IMPLEMENTATION
// Framework: .NET 10
// Status Schema: Draft / Printed / Disputed
// =====================================================

using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services.Dtos;
using System.Text.Json;

namespace NordicBeesERP.Services
{
    public class CreditNoteService : ICreditNoteService
    {
        private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;
        private readonly ICreditNoteNumberGenerator _numberGenerator;
        private readonly ICompanySettingsService _companySettings;
        private readonly IPdfGeneratorService _pdfGeneratorService;

        public CreditNoteService(
            IDbContextFactory<NordicBeesERPContext> contextFactory,
            ICreditNoteNumberGenerator numberGenerator,
            ICompanySettingsService companySettings,
            IPdfGeneratorService pdfGeneratorService)
        {
            _contextFactory = contextFactory;
            _numberGenerator = numberGenerator;
            _companySettings = companySettings;
            _pdfGeneratorService = pdfGeneratorService;
        }

        // =====================================================
        // LINE OPERATIONS
        // =====================================================

        public async Task<List<InvoiceLineDto>> GetInvoiceLinesAsync(int invoiceId)
        {
            using var context = _contextFactory.CreateDbContext();
            return await context.InvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .Select(l => new InvoiceLineDto
                {
                    Id = l.Id,
                    ProductCode = l.ProductCode,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    PriceExclVat = l.PriceExclVat,
                    VatRate = l.VatRate,
                    LineTotal = l.LineTotal
                })
                .OrderBy(l => l.Id)
                .ToListAsync();
        }

        // =====================================================
        // CREDITED QUANTITIES PER INVOICE LINE (all statuses incl. Draft)
        // =====================================================

        public async Task<Dictionary<int, decimal>> GetCreditedQuantitiesByInvoiceLineAsync(int invoiceId)
        {
            using var context = _contextFactory.CreateDbContext();

            var creditedLines = await context.CreditNoteLines
                .AsNoTracking()
                .Where(l => l.InvoiceLineId != null && l.InvoiceLine!.InvoiceId == invoiceId)
                .Select(l => new { InvoiceLineId = l.InvoiceLineId!.Value, l.Quantity })
                .ToListAsync();

            return creditedLines
                .GroupBy(l => l.InvoiceLineId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        }

        // =====================================================
        // CREDIT NOTES FOR A SPECIFIC INVOICE
        // =====================================================

        public async Task<List<CreditNoteListDto>> GetCreditNotesForInvoiceAsync(int invoiceId)
        {
            using var context = _contextFactory.CreateDbContext();

            return await context.CreditNotes
                .AsNoTracking()
                .Include(cn => cn.Customer)
                .Include(cn => cn.OriginalInvoice)
                .Include(cn => cn.AppliedInvoice)
                .Where(cn => cn.OriginalInvoiceId == invoiceId)
                .OrderByDescending(cn => cn.CreditDate)
                .Select(cn => new CreditNoteListDto
                {
                    Id = cn.Id,
                    CreditNoteNumber = cn.CreditNoteNumber,
                    OriginalInvoiceNumber = cn.OriginalInvoice != null ? cn.OriginalInvoice.InvoiceNumber : string.Empty,
                    AppliedInvoiceNumber = cn.AppliedInvoice != null ? cn.AppliedInvoice.InvoiceNumber : string.Empty,
                    CustomerName = cn.Customer != null ? cn.Customer.Name : string.Empty,
                    CreditDate = cn.CreditDate,
                    TotalInclVat = cn.TotalInclVat,
                    Status = cn.Status
                })
                .ToListAsync();
        }

        // =====================================================
        // INVOICE SELECTION FOR APPLYING
        // =====================================================

        public async Task<List<InvoiceSelectDto>> GetCustomerInvoicesForApplyingAsync(int customerId, int excludeInvoiceId)
        {
            using var context = _contextFactory.CreateDbContext();
            
            return await context.Invoices
                .Where(i => i.CustomerId == customerId && i.Id != excludeInvoiceId)
                .Select(i => new InvoiceSelectDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    TotalInclVat = i.TotalInclVat,
                    RemainingBalance = i.TotalInclVat - i.PaidAmount
                })
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();
        }

        // =====================================================
        // CREDIT NOTE CREATION
        // =====================================================

        public async Task<CreditNote> CreateCreditNoteAsync(CreateCreditNoteRequest request, int userId)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var creditNoteNumber = await _numberGenerator.GenerateNextNumberAsync(request.CreditDate);
            
            var creditNote = new CreditNote
            {
                CreditNoteNumber = creditNoteNumber,
                CreditDate = request.CreditDate,
                OriginalInvoiceId = request.OriginalInvoiceId,
                AppliedInvoiceId = request.AppliedInvoiceId ?? request.OriginalInvoiceId,
                CustomerId = request.OriginalInvoiceId > 0 
                    ? context.Invoices.Find(request.OriginalInvoiceId)?.CustomerId ?? 0 
                    : 0,
                CurrencyId = context.Invoices.Find(request.OriginalInvoiceId)?.CurrencyId ?? 1,
                Language = request.Language,
                ReverseCharge = false,
                SubtotalExclVat = 0,
                TotalVat = 0,
                TotalInclVat = 0,
                Status = CreditNoteStatus.Draft,
                Notes = request.Notes,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            context.CreditNotes.Add(creditNote);
            await context.SaveChangesAsync();
            
            // Process lines
            decimal subtotalExclVat = 0;
            decimal totalVat = 0;
            int lineNumber = 1;
            
            foreach (var lineRequest in request.Lines)
            {
                var invoiceLine = await context.InvoiceLines
                    .FirstOrDefaultAsync(l => l.Id == lineRequest.InvoiceLineId);
                
                if (invoiceLine == null)
                    continue;
                
                var lineQuantity = Math.Min(lineRequest.Quantity, invoiceLine.Quantity);
                
                if (lineQuantity <= 0)
                    continue;
                
                var lineSubtotal = Math.Round(lineQuantity * invoiceLine.PriceExclVat, 2);
                var vatAmount = Math.Round(lineSubtotal * invoiceLine.VatRate / 100, 2);
                var lineTotal = lineSubtotal + vatAmount;
                
                var line = new CreditNoteLine
                {
                    CreditNoteId = creditNote.Id,
                    InvoiceLineId = invoiceLine.Id,
                    LineNumber = lineNumber++,
                    ProductCode = invoiceLine.ProductCode,
                    Description = invoiceLine.Description,
                    Quantity = lineQuantity,
                    Unit = invoiceLine.Unit,
                    PriceExclVat = invoiceLine.PriceExclVat,
                    VatRate = invoiceLine.VatRate,
                    LineSubtotal = lineSubtotal,
                    VatAmount = vatAmount,
                    LineTotal = lineTotal,
                    LotNumber = invoiceLine.LotNumber,
                    CreatedAt = DateTime.UtcNow
                };
                
                context.CreditNoteLines.Add(line);
                
                subtotalExclVat += line.LineSubtotal;
                totalVat += line.VatAmount;
            }
            
            creditNote.SubtotalExclVat = subtotalExclVat;
            creditNote.TotalVat = totalVat;
            creditNote.TotalInclVat = subtotalExclVat + totalVat;
            creditNote.UpdatedAt = DateTime.UtcNow;
            
            await context.SaveChangesAsync();
            
            return creditNote;
        }

        // =====================================================
        // CREDIT NOTE LISTING (with pagination and filtering)
        // =====================================================

        public async Task<(List<CreditNoteListDto> Items, int TotalCount)> GetCreditNotesAsync(
            int currentPage, 
            int itemsPerPage, 
            string? filterSearch, 
            CreditNoteStatus? filterStatus, 
            DateTime? filterFromDate, 
            DateTime? filterToDate)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var query = context.CreditNotes
                .Include(cn => cn.Customer)
                .Include(cn => cn.OriginalInvoice)
                .Include(cn => cn.AppliedInvoice)
                .AsQueryable();
            
            // Filter by search (customer name, credit note number, or original invoice number)
            if (!string.IsNullOrEmpty(filterSearch))
            {
                query = query.Where(cn => cn.Customer.Name.Contains(filterSearch) 
                                       || cn.CreditNoteNumber.Contains(filterSearch)
                                       || (cn.OriginalInvoice != null && cn.OriginalInvoice.InvoiceNumber.Contains(filterSearch)));
            }
            
            // Filter by status
            if (filterStatus.HasValue)
            {
                query = query.Where(cn => cn.Status == filterStatus.Value);
            }
            
            // Filter by date range
            if (filterFromDate.HasValue)
            {
                query = query.Where(cn => cn.CreditDate >= filterFromDate.Value);
            }
            
            if (filterToDate.HasValue)
            {
                query = query.Where(cn => cn.CreditDate <= filterToDate.Value);
            }
            
            var totalCount = await query.CountAsync();
            
            var items = await query
                .OrderByDescending(cn => cn.CreatedAt)
                .Skip((currentPage - 1) * itemsPerPage)
                .Take(itemsPerPage)
                .Select(cn => new CreditNoteListDto
                {
                    Id = cn.Id,
                    CreditNoteNumber = cn.CreditNoteNumber,
                    OriginalInvoiceNumber = cn.OriginalInvoice != null ? cn.OriginalInvoice.InvoiceNumber : string.Empty,
                    AppliedInvoiceNumber = cn.AppliedInvoice != null ? cn.AppliedInvoice.InvoiceNumber : string.Empty,
                    CustomerName = cn.Customer != null ? cn.Customer.Name : string.Empty,
                    CreditDate = cn.CreditDate,
                    TotalInclVat = cn.TotalInclVat,
                    Status = cn.Status
                })
                .ToListAsync();
            
            return (items, totalCount);
        }

        // =====================================================
        // CREDIT NOTE RETRIEVAL
        // =====================================================

        public async Task<HashSet<int>> GetInvoiceIdsWithCreditNotesAsync(IEnumerable<int> invoiceIds)
        {
            using var context = _contextFactory.CreateDbContext();
            var idList = invoiceIds.ToList();
            if (idList.Count == 0) return new HashSet<int>();
            var ids = await context.CreditNotes
                .AsNoTracking()
                .Where(cn => cn.OriginalInvoiceId != null && idList.Contains(cn.OriginalInvoiceId.Value))
                .Select(cn => cn.OriginalInvoiceId!.Value)
                .Distinct()
                .ToListAsync();
            return ids.ToHashSet();
        }

        public async Task<CreditNoteDetailDto> GetCreditNoteAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var creditNote = await context.CreditNotes
                .Include(cn => cn.Customer)
                .Include(cn => cn.Currency)
                .Include(cn => cn.OriginalInvoice)
                .Include(cn => cn.AppliedInvoice)
                .Include(cn => cn.Lines)
                .FirstOrDefaultAsync(cn => cn.Id == id);
            
            if (creditNote == null)
                throw new InvalidOperationException($"Credit note with ID {id} not found.");
            
            var lines = await context.CreditNoteLines
                .Where(l => l.CreditNoteId == id)
                .Select(l => new CreditNoteLineDto
                {
                    Id = l.Id,
                    CreditNoteId = l.CreditNoteId,
                    InvoiceLineId = l.InvoiceLineId,
                    InvoiceLineDescription = l.InvoiceLine != null ? l.InvoiceLine.Description : string.Empty,
                    LineNumber = l.LineNumber,
                    ProductCode = l.ProductCode,
                    Description = l.Description,
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    PriceExclVat = l.PriceExclVat,
                    VatRate = l.VatRate,
                    LineSubtotal = l.LineSubtotal,
                    VatAmount = l.VatAmount,
                    LineTotal = l.LineTotal,
                    LotNumber = l.LotNumber,
                    CreatedAt = l.CreatedAt
                })
                .OrderBy(l => l.LineNumber)
                .ToListAsync();
            
            // Get payment info from original invoice
            int paymentTermDays = 0;
            DateTime? paymentDueDate = null;
            if (creditNote.OriginalInvoice != null)
            {
                paymentTermDays = creditNote.OriginalInvoice.PaymentTermDays;
                paymentDueDate = creditNote.OriginalInvoice.PaymentDueDate;
            }
            
            return new CreditNoteDetailDto
            {
                Id = creditNote.Id,
                CreditNoteNumber = creditNote.CreditNoteNumber,
                CreditDate = creditNote.CreditDate,
                OriginalInvoiceId = creditNote.OriginalInvoiceId,
                OriginalInvoiceNumber = creditNote.OriginalInvoice != null ? creditNote.OriginalInvoice.InvoiceNumber : string.Empty,
                AppliedInvoiceId = creditNote.AppliedInvoiceId,
                AppliedInvoiceNumber = creditNote.AppliedInvoice != null ? creditNote.AppliedInvoice.InvoiceNumber : string.Empty,
                CustomerId = creditNote.CustomerId,
                CustomerName = creditNote.Customer != null ? creditNote.Customer.Name : string.Empty,
                CustomerEmail = creditNote.Customer != null ? creditNote.Customer.Email : null,
                CustomerPhone = creditNote.Customer != null ? creditNote.Customer.Phone : null,
                CustomerAddress = creditNote.Customer != null ? creditNote.Customer.Address : null,
                CurrencyId = creditNote.CurrencyId,
                CurrencyCode = creditNote.Currency != null ? creditNote.Currency.Code : string.Empty,
                Language = creditNote.Language,
                ReverseCharge = creditNote.ReverseCharge,
                SubtotalExclVat = creditNote.SubtotalExclVat,
                TotalVat = creditNote.TotalVat,
                TotalInclVat = creditNote.TotalInclVat,
                Status = creditNote.Status,
                PdfPath = creditNote.PdfPath,
                Notes = creditNote.Notes,
                CreatedBy = creditNote.CreatedBy,
                CreatedByName = "",
                CreatedAt = creditNote.CreatedAt,
                UpdatedAt = creditNote.UpdatedAt,
                PaymentTermDays = paymentTermDays,
                PaymentDueDate = paymentDueDate,
                Lines = lines
            };
        }

        // =====================================================
        // SET PRINTED STATUS - allow setting Printed regardless of current status
        // =====================================================

        public async Task SetPrintedAsync(int id)
        {
            using var context = await _contextFactory.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE credit_notes SET status = 'printed', updated_at = NOW() WHERE id = {0}", id);
        }

        // =====================================================
        // SET DISPUTED STATUS
        // =====================================================
        // GET CREDIT NOTE NUMBER FOR DATE
        // =====================================================

        public async Task<string> GetNextCreditNoteNumberAsync(DateTime creditDate)
        {
            return await _numberGenerator.GenerateNextNumberAsync(creditDate);
        }

        // =====================================================
        // INVOICE SEARCH FOR AUTOCOMPLETE (Credit Note Dialog)
        // =====================================================

        public async Task<List<InvoiceModel>> SearchInvoicesAsync(string searchTerm, int limit = 20, CancellationToken cancellationToken = default)
        {
            using var context = _contextFactory.CreateDbContext();

            var currentYear = DateTime.UtcNow.Year;
            var yearStart = new DateTime(currentYear, 1, 1);
            var yearEnd = new DateTime(currentYear, 12, 31, 23, 59, 59);

            var query = context.Invoices
                .Where(i => i.InvoiceDate >= yearStart && i.InvoiceDate <= yearEnd)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchTerm) ||
                                        i.Customer.Name.Contains(searchTerm));
            }

            return await query
                .OrderByDescending(i => i.InvoiceDate)
                .Take(limit)
                .Select(i => new InvoiceModel
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    TotalInclVat = i.TotalInclVat,
                    CustomerId = i.CustomerId,
                    CustomerName = i.Customer.Name,
                    CurrencyId = i.CurrencyId ?? 0,
                    CurrencyCode = i.Currency == null ? string.Empty : i.Currency.Code,
                    Status = i.Status.ToString()
                })
                .ToListAsync(cancellationToken);
        }

        // =====================================================
        // INVOICE SEARCH FOR AUTOCOMPLETE (Credit Note Page)
        // =====================================================

        public async Task<List<InvoiceSelectDto>> SearchInvoicesForCreditNoteAsync(string searchTerm, int customerId, int limit = 20, CancellationToken cancellationToken = default)
        {
            using var context = _contextFactory.CreateDbContext();

            var currentYear = DateTime.UtcNow.Year;
            var yearStart = new DateTime(currentYear, 1, 1);
            var yearEnd = new DateTime(currentYear, 12, 31, 23, 59, 59);

            var query = context.Invoices
                .Where(i => i.InvoiceDate >= yearStart && i.InvoiceDate <= yearEnd && (customerId == 0 || i.CustomerId == customerId))
                .AsNoTracking();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(i => i.InvoiceNumber.Contains(searchTerm));
            }

            return await query
                .OrderByDescending(i => i.InvoiceDate)
                .Take(limit)
                .Select(i => new InvoiceSelectDto
                {
                    Id = i.Id,
                    InvoiceNumber = i.InvoiceNumber,
                    InvoiceDate = i.InvoiceDate,
                    TotalInclVat = i.TotalInclVat,
                    RemainingBalance = i.TotalInclVat - i.PaidAmount,
                    CustomerName = i.Customer.Name,
                    CustomerId = i.CustomerId,
                    CustomerDefaultLanguage = i.Customer.DefaultLanguage
                })
                .ToListAsync(cancellationToken);
        }

        // =====================================================
        // CREDIT NOTE UPDATE (EDIT) - Draft only
        // =====================================================

        public async Task UpdateCreditNoteAsync(UpdateCreditNoteRequest request, int? userId)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var creditNote = await context.CreditNotes
                .Include(cn => cn.Lines)
                .FirstOrDefaultAsync(cn => cn.Id == request.Id);
            
            if (creditNote == null)
                throw new InvalidOperationException($"Credit note with ID {request.Id} not found.");
            
            if (creditNote.Status != CreditNoteStatus.Draft)
                throw new InvalidOperationException("Only draft credit notes can be edited.");
            
            // Update basic fields
            creditNote.CreditDate = request.CreditDate;
            creditNote.OriginalInvoiceId = request.OriginalInvoiceId;
            creditNote.AppliedInvoiceId = request.AppliedInvoiceId ?? creditNote.AppliedInvoiceId;
            creditNote.Language = request.Language;
            creditNote.ReverseCharge = request.ReverseCharge;
            creditNote.Notes = request.Notes;
            
            // Get new customer and currency from original invoice (read-only lookup)
            var originalInvoice = await context.Invoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == request.OriginalInvoiceId);
            if (originalInvoice != null)
            {
                creditNote.CustomerId = originalInvoice.CustomerId;
                creditNote.CurrencyId = originalInvoice.CurrencyId ?? 1;
            }
            
            // Remove existing lines
            context.CreditNoteLines.RemoveRange(creditNote.Lines);
            
            // Recalculate lines
            decimal newSubtotalExclVat = 0;
            decimal newTotalVat = 0;
            int newLineNumber = 1;
            
            foreach (var lineRequest in request.Lines)
            {
                var invoiceLine = await context.InvoiceLines
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == lineRequest.InvoiceLineId);
                
                if (invoiceLine == null)
                    continue;
                
                var lineQuantity = Math.Min(lineRequest.Quantity, invoiceLine.Quantity);
                
                if (lineQuantity <= 0)
                    continue;
                
                var lineSubtotal = Math.Round(lineQuantity * invoiceLine.PriceExclVat, 2);
                var vatAmount = Math.Round(lineSubtotal * invoiceLine.VatRate / 100, 2);
                var lineTotal = lineSubtotal + vatAmount;
                
                var line = new CreditNoteLine
                {
                    CreditNoteId = creditNote.Id,
                    InvoiceLineId = invoiceLine.Id,
                    LineNumber = newLineNumber++,
                    ProductCode = invoiceLine.ProductCode,
                    Description = invoiceLine.Description,
                    Quantity = lineQuantity,
                    Unit = invoiceLine.Unit,
                    PriceExclVat = invoiceLine.PriceExclVat,
                    VatRate = invoiceLine.VatRate,
                    LineSubtotal = lineSubtotal,
                    VatAmount = vatAmount,
                    LineTotal = lineTotal,
                    LotNumber = invoiceLine.LotNumber,
                    CreatedAt = DateTime.UtcNow
                };
                
                context.CreditNoteLines.Add(line);
                
                newSubtotalExclVat += line.LineSubtotal;
                newTotalVat += line.VatAmount;
            }
            
            var newTotalInclVat = newSubtotalExclVat + newTotalVat;
            var newUpdatedAt = DateTime.UtcNow;

            // Persist line removals/additions first
            await context.SaveChangesAsync();

            // Then update the credit note header via raw SQL
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE credit_notes SET
                    credit_date = {0}, original_invoice_id = {1}, applied_invoice_id = {2},
                    language = {3}, reverse_charge = {4}, notes = {5},
                    customer_id = {6}, currency_id = {7},
                    subtotal_excl_vat = {8}, total_vat = {9}, total_incl_vat = {10},
                    updated_at = {11}
                WHERE id = {12}",
                creditNote.CreditDate, creditNote.OriginalInvoiceId, creditNote.AppliedInvoiceId,
                creditNote.Language, creditNote.ReverseCharge, creditNote.Notes,
                creditNote.CustomerId, creditNote.CurrencyId,
                newSubtotalExclVat, newTotalVat, newTotalInclVat,
                newUpdatedAt, creditNote.Id);
        }

        // =====================================================
        // GENERATE PDF (used by API controller)
        // =====================================================

        public async Task<byte[]> GeneratePdfAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var creditNote = await context.CreditNotes
                .Include(cn => cn.Customer)
                .Include(cn => cn.Currency)
                .Include(cn => cn.OriginalInvoice)
                .Include(cn => cn.Lines)
                .FirstOrDefaultAsync(cn => cn.Id == id);
            
            if (creditNote == null)
                throw new InvalidOperationException($"Credit note with ID {id} not found.");
            
            var creditNoteLines = await context.CreditNoteLines
                .Where(l => l.CreditNoteId == id)
                .OrderBy(l => l.LineNumber)
                .ToListAsync();
            
            var lines = creditNoteLines.Select(l => new CreditNoteLineDto
            {
                Id = l.Id,
                CreditNoteId = l.CreditNoteId,
                InvoiceLineId = l.InvoiceLineId,
                InvoiceLineDescription = l.InvoiceLine != null ? l.InvoiceLine.Description : string.Empty,
                LineNumber = l.LineNumber,
                ProductCode = l.ProductCode,
                Description = l.Description,
                Quantity = l.Quantity,
                Unit = l.Unit,
                PriceExclVat = l.PriceExclVat,
                VatRate = l.VatRate,
                LineSubtotal = l.LineSubtotal,
                VatAmount = l.VatAmount,
                LineTotal = l.LineTotal,
                LotNumber = l.LotNumber,
                CreatedAt = l.CreatedAt
            }).ToList();
            
            // Fetch customer and currency
            var customer = creditNote.Customer;
            var currency = creditNote.Currency;
            var originalInvoiceNumber = creditNote.OriginalInvoice?.InvoiceNumber;
            var originalInvoiceDate = creditNote.OriginalInvoice?.InvoiceDate;
            var appliedInvoiceNumber = creditNote.AppliedInvoice?.InvoiceNumber;
            var createdByName = "";
            if (creditNote.Customer != null && !string.IsNullOrEmpty(creditNote.Customer.DefaultLanguage))
                creditNote.Language = creditNote.Customer.DefaultLanguage;

            return await _pdfGeneratorService.GenerateCreditNotePdfAsync(
                creditNote, 
                lines, 
                customer, 
                currency, 
                originalInvoiceNumber, 
                originalInvoiceDate, 
                appliedInvoiceNumber, 
                createdByName);
        }

        // =====================================================
        // CREDIT NOTE DELETE
        // =====================================================

        public async Task DeleteCreditNoteAsync(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            
            var creditNote = await context.CreditNotes
                .Include(cn => cn.Lines)
                .FirstOrDefaultAsync(cn => cn.Id == id);
            
            if (creditNote == null)
                throw new InvalidOperationException($"Credit note with ID {id} not found.");
            
            if (creditNote.Status != CreditNoteStatus.Draft)
                throw new InvalidOperationException("Only draft credit notes can be deleted.");
            
            context.CreditNoteLines.RemoveRange(creditNote.Lines);
            context.CreditNotes.Remove(creditNote);
            
            await context.SaveChangesAsync();
        }

    }
}
