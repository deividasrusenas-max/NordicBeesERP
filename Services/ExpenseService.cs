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
        private readonly ICompanySettingsService _companySettingsService;

        public ExpenseService(IDbContextFactory<NordicBeesERPContext> dbFactory, IAuthService authService, ICompanySettingsService companySettingsService)
        {
            _dbFactory = dbFactory;
            _authService = authService;
            _companySettingsService = companySettingsService;
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

        public async Task<List<ExpenseInvoice>> SearchExpenseInvoicesAsync(string searchTerm, int limit = 20)
        {
            await using var context = _dbFactory.CreateDbContext();

            // Diacritic- and case-insensitive search: fold the term (e.g. "zukline" matches "Žūklinė")
            var foldedTerm = DiacriticHelper.Fold(searchTerm);

            // expense_invoices is tiny (~13 rows) and its collation is accent-sensitive,
            // so server-side LIKE cannot do diacritic-insensitive matching — materialize
            // the whole table and fold in memory (same pattern as CustomerService.SearchBusinessPartnersAsync).
            var invoices = await context.ExpenseInvoices
                .AsNoTracking()
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();

            // Populate SupplierName from BusinessPartners (NotMapped — no Include allowed)
            var supplierIds = invoices
                .Where(i => i.SupplierId.HasValue)
                .Select(i => i.SupplierId.Value)
                .Distinct()
                .ToList();

            if (supplierIds.Count > 0)
            {
                var suppliers = await context.BusinessPartners
                    .AsNoTracking()
                    .Where(s => supplierIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name);

                foreach (var invoice in invoices)
                {
                    if (invoice.SupplierId.HasValue && suppliers.TryGetValue(invoice.SupplierId.Value, out var supplierName))
                        invoice.SupplierName = supplierName;
                }
            }

            if (!string.IsNullOrEmpty(foldedTerm))
            {
                invoices = invoices
                    .Where(i => (i.InvoiceNumber != null && DiacriticHelper.Fold(i.InvoiceNumber).Contains(foldedTerm))
                        || (i.PendingSupplierName != null && DiacriticHelper.Fold(i.PendingSupplierName).Contains(foldedTerm))
                        || (!string.IsNullOrEmpty(i.SupplierName) && DiacriticHelper.Fold(i.SupplierName).Contains(foldedTerm)))
                    .ToList();
            }

            return invoices.Take(limit).ToList();
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
                
                // Update invoice totals based on lines (raw SQL — NoTracking means IsModified won't work)
                var amountExclVat = lines.Sum(l => l.AmountExclVat);
                var vatAmount = lines.Sum(l => l.AmountInclVat - l.AmountExclVat);
                var amountInclVat = lines.Sum(l => l.AmountInclVat);
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE expense_invoices SET amount_excl_vat = {0}, vat_amount = {1}, amount_incl_vat = {2} WHERE id = {3}",
                    amountExclVat, vatAmount, amountInclVat, invoice.Id);
            }
            
            // Log invoice creation
            await LogAuditAsync(context, invoice.Id, invoice.InvoiceNumber, "UPLOADED");
            
            return new InvoiceAddResult { IsDuplicate = false, OriginalInvoiceId = 0, ThisInvoiceId = invoice.Id };
        }

        public async Task<ExpenseInvoice> UpdateInvoiceAsync(ExpenseInvoice invoice, List<string>? overriddenFlags = null)
        {
            using var context = _dbFactory.CreateDbContext();

            // The old status, supplier and flags come from the DB — never from the caller's object.
            // This method derives the new status; explicit transitions go through dedicated methods
            // (Approve, Reject, ResolveDuplicateAsDifferent, DismissWrongRecipient, Restore).
            var stored = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoice.Id);
            if (stored == null)
                throw new InvalidOperationException($"Sąskaita #{invoice.Id} nerasta");

            invoice.UpdatedAt = DateTime.UtcNow;
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    invoice_number = {0},
                    invoice_date = {1},
                    due_date = {2},
                    amount_excl_vat = {3},
                    vat_rate = {4},
                    vat_amount = {5},
                    amount_incl_vat = {6},
                    notes = {7},
                    updated_at = {8}
                WHERE id = {9}",
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.AmountExclVat,
                invoice.VatRate,
                invoice.VatAmount,
                invoice.AmountInclVat,
                invoice.Notes,
                invoice.UpdatedAt,
                invoice.Id);

            // Recalculate OCR flags after saving invoice changes
            var lines = await context.ExpenseInvoiceLines.Where(l => l.InvoiceId == invoice.Id).ToListAsync();
            var existing = ExpenseStatusHelper.ParseFlags(stored.OcrFlags);
            var flags = ComputeManualEditFlags(invoice, stored, lines, existing, overriddenFlags);

            // supplier_id is not written by this method, so it cannot change here.
            var changedGateFields = ChangedGateFields(invoice, stored);
            var currentUser = await _authService.GetAuthenticatedUserAsync();
            var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "MANUAL_EDIT";
            await ApplyManualEditStatusAsync(context, invoice, stored, flags, changedGateFields, performedBy);

            return invoice;
        }

        /// <summary>
        /// Edit-form save (D-035): header + lines + flags + status + audit in ONE transaction.
        /// Header amounts are authoritative and are never overwritten from line sums; a lines/header
        /// mismatch becomes AMOUNT_MISMATCH. Lines are upserted by id so existing lines keep their
        /// id (and their category allocations — FK ON DELETE CASCADE); only lines removed in the form
        /// are deleted. Flags and status are evaluated on the final stored values.
        /// </summary>
        public async Task<ExpenseInvoice> SaveInvoiceEditAsync(ExpenseInvoice invoice, List<ExpenseInvoiceLine> lines, string performedBy)
        {
            await using var context = _dbFactory.CreateDbContext();
            await using var transaction = await context.Database.BeginTransactionAsync();

            var stored = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoice.Id);
            if (stored == null)
                throw new InvalidOperationException($"Sąskaita #{invoice.Id} nerasta");
            var storedLines = await context.ExpenseInvoiceLines
                .AsNoTracking()
                .Where(l => l.InvoiceId == invoice.Id)
                .ToListAsync();

            invoice.UpdatedAt = DateTime.UtcNow;
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    invoice_number = {0},
                    invoice_date = {1},
                    due_date = {2},
                    amount_excl_vat = {3},
                    vat_rate = {4},
                    vat_amount = {5},
                    amount_incl_vat = {6},
                    notes = {7},
                    category_id = {8},
                    updated_at = {9}
                WHERE id = {10}",
                invoice.InvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.AmountExclVat,
                invoice.VatRate,
                invoice.VatAmount,
                invoice.AmountInclVat,
                invoice.Notes,
                invoice.CategoryId,
                invoice.UpdatedAt,
                invoice.Id);

            // Lines: upsert by id; delete only the lines the user removed in the form
            var storedIds = storedLines.Select(l => l.Id).ToHashSet();
            var keptIds = lines.Where(l => l.Id > 0).Select(l => l.Id).ToHashSet();
            if (!keptIds.IsSubsetOf(storedIds))
                throw new InvalidOperationException("Eilutė nepriklauso šiai sąskaitai");
            foreach (var removedId in storedIds.Except(keptIds))
            {
                await context.Database.ExecuteSqlRawAsync(
                    "DELETE FROM expense_invoice_lines WHERE id = {0} AND invoice_id = {1}", removedId, invoice.Id);
            }
            var storedById = storedLines.ToDictionary(l => l.Id);
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                line.InvoiceId = invoice.Id;
                line.SortOrder = i + 1;
                // Gross is derived only when the line is new or its net/VAT rate changed; an untouched
                // line keeps its stored gross (OCR may differ by a cent), so a notes-only save never
                // counts as a line change for the approval rule.
                if (line.Id > 0 && storedById.TryGetValue(line.Id, out var storedLine)
                    && storedLine.AmountExclVat == line.AmountExclVat && storedLine.VatRate == line.VatRate)
                    line.AmountInclVat = storedLine.AmountInclVat;
                else
                    line.AmountInclVat = Math.Round(line.AmountExclVat * (1 + line.VatRate / 100), 2);
                if (line.Id > 0)
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        UPDATE expense_invoice_lines SET
                            description = {0},
                            quantity = {1},
                            unit_price = {2},
                            amount_excl_vat = {3},
                            vat_rate = {4},
                            amount_incl_vat = {5},
                            sort_order = {6}
                        WHERE id = {7} AND invoice_id = {8}",
                        line.Description, line.Quantity, line.UnitPrice, line.AmountExclVat, line.VatRate,
                        line.AmountInclVat, line.SortOrder, line.Id, invoice.Id);
                }
                else
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        INSERT INTO expense_invoice_lines
                            (invoice_id, description, quantity, unit_price, amount_excl_vat, vat_rate, amount_incl_vat, sort_order)
                        VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                        invoice.Id, line.Description, line.Quantity, line.UnitPrice, line.AmountExclVat, line.VatRate,
                        line.AmountInclVat, line.SortOrder);
                }
            }

            var finalLines = await context.ExpenseInvoiceLines
                .AsNoTracking()
                .Where(l => l.InvoiceId == invoice.Id)
                .ToListAsync();
            var existing = ExpenseStatusHelper.ParseFlags(stored.OcrFlags);
            var flags = ComputeManualEditFlags(invoice, stored, finalLines, existing, null);

            var changedGateFields = ChangedGateFields(invoice, stored);
            if (LinesChanged(storedLines, finalLines)) changedGateFields.Add("lines");
            var newStatus = await ApplyManualEditStatusAsync(context, invoice, stored, flags, changedGateFields, performedBy);

            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO expense_invoice_audit
                    (invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at)
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                invoice.Id, invoice.InvoiceNumber, "EDITED",
                changedGateFields.Count > 0 ? $"Pakeisti laukai: {string.Join(", ", changedGateFields)}" : "Pakeisti tik nekontroliuojami laukai",
                stored.Status, newStatus, performedBy, DateTime.Now);

            await transaction.CommitAsync();
            return invoice;
        }

        /// <summary>True when the set of lines or any line's amounts / VAT rate changed (gate-relevant).</summary>
        private static bool LinesChanged(List<ExpenseInvoiceLine> before, List<ExpenseInvoiceLine> after)
        {
            if (before.Count != after.Count) return true;
            var byId = before.ToDictionary(l => l.Id);
            foreach (var line in after)
            {
                if (!byId.TryGetValue(line.Id, out var old)) return true;
                if (old.AmountExclVat != line.AmountExclVat || old.VatRate != line.VatRate || old.AmountInclVat != line.AmountInclVat)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Stores flags and the manual-edit status. An approval covers the values that were approved:
        /// with no gate-relevant change the status stands (notes/category edits never re-open review);
        /// otherwise the C2 rules apply and a NEEDS_REVIEW result voids the approval (audited).
        /// </summary>
        private static async Task<string> ApplyManualEditStatusAsync(NordicBeesERPContext context, ExpenseInvoice invoice,
            ExpenseInvoice stored, List<string> flags, List<string> changedGateFields, string performedBy)
        {
            var approved = !string.IsNullOrEmpty(stored.ApprovedBy);
            var newStatus = approved && changedGateFields.Count == 0
                ? stored.Status
                : StatusAfterManualEdit(stored.Status, flags, stored.SupplierId);
            var voidApproval = approved && changedGateFields.Count > 0 && newStatus == "NEEDS_REVIEW";

            invoice.OcrFlags = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            invoice.Status = newStatus;
            invoice.SupplierId = stored.SupplierId;

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_invoices SET ocr_flags = {0}, status = {1}, updated_at = {2} WHERE id = {3}",
                invoice.OcrFlags, newStatus, DateTime.UtcNow, invoice.Id);

            if (voidApproval)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE expense_invoices SET approved_by = NULL, approved_at = NULL WHERE id = {0}",
                    invoice.Id);
                await context.Database.ExecuteSqlRawAsync(@"
                    INSERT INTO expense_invoice_audit
                        (invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at)
                    VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                    invoice.Id, invoice.InvoiceNumber, "APPROVAL_VOIDED",
                    $"Pakeisti laukai: {string.Join(", ", changedGateFields)}",
                    stored.Status, newStatus, performedBy, DateTime.Now);
                invoice.ApprovedBy = null;
                invoice.ApprovedAt = null;
            }

            return newStatus;
        }

        /// <summary>
        /// Gate-relevant header fields that differ between the edited values and the stored row
        /// (DB column names). Used to decide whether a prior approval still covers the invoice.
        /// </summary>
        private static List<string> ChangedGateFields(ExpenseInvoice edited, ExpenseInvoice stored)
        {
            var changed = new List<string>();
            if ((edited.InvoiceNumber ?? "") != (stored.InvoiceNumber ?? "")) changed.Add("invoice_number");
            if (edited.InvoiceDate.Date != stored.InvoiceDate.Date) changed.Add("invoice_date");
            if (edited.DueDate.Date != stored.DueDate.Date) changed.Add("due_date");
            if (edited.AmountExclVat != stored.AmountExclVat) changed.Add("amount_excl_vat");
            if (edited.VatRate != stored.VatRate) changed.Add("vat_rate");
            if (edited.VatAmount != stored.VatAmount) changed.Add("vat_amount");
            if (edited.AmountInclVat != stored.AmountInclVat) changed.Add("amount_incl_vat");
            return changed;
        }

        /// <summary>
        /// Flags the manual-edit path owns: recomputed from the values being saved, or dropped/kept by
        /// an explicit rule below. Every other stored flag (OWN_COMPANY, INVALID_VAT_RATE,
        /// VIES_UNAVAILABLE, DUPLICATE, any future code, …) is a fact about the document or an earlier
        /// step that an edit cannot re-derive, so it is carried over unchanged — an unknown flag
        /// survives by default instead of vanishing (PLAN-ETAPAS1 §5, the 370 OWN_COMPANY finding).
        /// </summary>
        private static readonly HashSet<string> ManualEditOwnedFlags = new()
        {
            // recomputed from the edited values
            OcrFlag.MissingInvNumber, OcrFlag.MissingAmount, OcrFlag.ZeroVat, OcrFlag.LinesNotFound,
            OcrFlag.AmountMismatch, OcrFlag.AmountArithmeticMismatch, OcrFlag.MissingMoneyField,
            OcrFlag.FutureDate, OcrFlag.StaleDate, OcrFlag.MissingInvDate,
            // explicit keep/drop rules
            OcrFlag.WrongRecipient, OcrFlag.VendorNotFound, OcrFlag.MissingDueDate, OcrFlag.LowConfidence
        };

        /// <summary>
        /// Flags after a manual edit (C2 rules): the owned flags are recomputed from the values being
        /// saved; all other stored flags are carried over (<see cref="ManualEditOwnedFlags"/>).
        /// </summary>
        private static List<string> ComputeManualEditFlags(ExpenseInvoice edited, ExpenseInvoice stored,
            List<ExpenseInvoiceLine> lines, List<string> existing, List<string>? overriddenFlags)
        {
            var flags = new List<string>();
            if (string.IsNullOrEmpty(edited.InvoiceNumber)) flags.Add(OcrFlag.MissingInvNumber);
            if (edited.AmountInclVat == 0) flags.Add(OcrFlag.MissingAmount);
            if (edited.VatRate == 0 && edited.AmountInclVat > 0) flags.Add(OcrFlag.ZeroVat);
            if (lines.Count == 0) flags.Add(OcrFlag.LinesNotFound);
            // D-019/D-035: header is authoritative; lines disagreeing with it (net, 0.01) are flagged, never copied over
            if (lines.Count > 0 && Math.Abs(lines.Sum(l => l.AmountExclVat) - edited.AmountExclVat) > 0.01m)
                flags.Add(OcrFlag.AmountMismatch);
            RecomputeAmountConsistencyFlags(flags, edited.AmountExclVat, edited.VatAmount, edited.AmountInclVat);
            RecomputeDateFlags(flags, edited.InvoiceDate != default ? edited.InvoiceDate : null,
                stored.CreatedAt != default ? stored.CreatedAt : VilniusToday());

            // Owned flags with explicit keep/drop rules
            bool wrongRecipientDismissed = overriddenFlags != null && !overriddenFlags.Contains(OcrFlag.WrongRecipient);
            if (!wrongRecipientDismissed && existing.Contains(OcrFlag.WrongRecipient))
                flags.Add(OcrFlag.WrongRecipient);
            if (existing.Contains(OcrFlag.VendorNotFound) && stored.SupplierId == null) flags.Add(OcrFlag.VendorNotFound);

            // D-025: the due date stays marked as assumed until the user actually changes it.
            if (existing.Contains(OcrFlag.MissingDueDate) && edited.DueDate.Date == stored.DueDate.Date)
                flags.Add(OcrFlag.MissingDueDate);

            // LOW_CONFIDENCE is deliberately NOT carried over: a human has just reviewed and saved
            // these values, and an OCR confidence score the user cannot change must not lock the
            // invoice in NEEDS_REVIEW.

            // Everything the edit path does not own is carried over as stored (order kept, no duplicates)
            foreach (var flag in existing)
                if (!ManualEditOwnedFlags.Contains(flag) && !flags.Contains(flag))
                    flags.Add(flag);

            return flags;
        }

        /// <summary>
        /// Status after a manual edit, by the old (stored) status. Open invoices are recomputed:
        /// no supplier → PENDING_SUPPLIER; review flag or WRONG_RECIPIENT → NEEDS_REVIEW; else PENDING.
        /// Paid, quarantined and other statuses are left unchanged.
        /// </summary>
        private static string StatusAfterManualEdit(string oldStatus, List<string> flags, int? supplierId)
        {
            if (oldStatus is not ("PENDING" or "NEEDS_REVIEW" or "PENDING_SUPPLIER"))
                return oldStatus;
            if (supplierId == null) return "PENDING_SUPPLIER";
            // WRONG_RECIPIENT keeps its previous manual-edit behaviour: it holds the invoice in
            // review (it never rejects on edit); it is cleared only by DismissWrongRecipientAsync.
            if (HasReviewFlag(flags) || flags.Contains(OcrFlag.WrongRecipient)) return "NEEDS_REVIEW";
            return "PENDING";
        }

        public async Task DismissWrongRecipientAsync(int invoiceId, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();

            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Sąskaita #{invoiceId} nerasta");

            var flags = ExpenseStatusHelper.ParseFlags(invoice.OcrFlags);
            flags.RemoveAll(f => f == OcrFlag.WrongRecipient);

            var oldStatus = invoice.Status;
            var rejectedForRecipient = oldStatus == "REJECTED" && invoice.RejectedReason?.StartsWith("Sąskaita ne ") == true;
            var rejectedReason = rejectedForRecipient ? null : invoice.RejectedReason;
            var newStatus = rejectedForRecipient || oldStatus is "PENDING" or "NEEDS_REVIEW" or "PENDING_SUPPLIER"
                ? DecideOcrStatus(flags, invoice.SupplierId)
                : oldStatus;

            var ocrFlagsJson = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            var now = DateTime.Now;

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    ocr_flags = {0},
                    status = {1},
                    rejected_reason = {2},
                    updated_at = {3}
                WHERE id = {4}",
                ocrFlagsJson, newStatus, rejectedReason, now, invoiceId);

            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO expense_invoice_audit
                    (invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at)
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                invoiceId, invoice.InvoiceNumber, "WRONG_RECIPIENT_DISMISSED",
                "Gavėjas patvirtintas rankiniu būdu",
                oldStatus, newStatus, performedBy, now);
        }

        public async Task<bool> DeleteInvoiceAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseInvoices
                .AsNoTracking()
                .AnyAsync(i => i.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_invoices WHERE id = {0}",
                id);
            
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
            
            // Recalculate totals
            line.AmountInclVat = line.AmountExclVat * (1 + line.VatRate / 100);
            
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoice_lines SET
                    description = {0},
                    amount_excl_vat = {1},
                    vat_rate = {2},
                    amount_incl_vat = {3},
                    sort_order = {4}
                WHERE id = {5}",
                line.Description,
                line.AmountExclVat,
                line.VatRate,
                line.AmountInclVat,
                line.SortOrder,
                line.Id);
            
            return line;
        }

        public async Task<bool> DeleteInvoiceLineAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseInvoiceLines
                .AsNoTracking()
                .AnyAsync(l => l.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_invoice_lines WHERE id = {0}",
                id);
            
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
            
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_line_allocations SET
                    category_id = {0},
                    cost_center_id = {1},
                    allocated_amount = {2},
                    allocated_percent = {3}
                WHERE id = {4}",
                allocation.CategoryId,
                allocation.CostCenterId,
                allocation.AllocatedAmount,
                allocation.AllocatedPercent,
                allocation.Id);
            
            return allocation;
        }

        public async Task<bool> DeleteAllocationAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseLineAllocations
                .AsNoTracking()
                .AnyAsync(a => a.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_line_allocations WHERE id = {0}",
                id);
            
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
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == payment.InvoiceId);
            var oldStatus = invoice?.Status;
            
            payment.CreatedAt = DateTime.UtcNow;
            await context.ExpensePayments.AddAsync(payment);
            await context.SaveChangesAsync();
            
            // Recalculate paid amount and update status
            await RecalculateInvoiceStatusAsync(payment.InvoiceId);
            
            // Get new status after recalculation (re-query — ReloadAsync on detached entity silently fails)
            var updatedInvoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == payment.InvoiceId);
            var newStatus = updatedInvoice?.Status;
            
            // Log payment added
            await LogAuditAsync(context, payment.InvoiceId, invoice?.InvoiceNumber ?? "Unknown", "PAYMENT_ADDED", 
                $"Amount: {payment.Amount:C}", oldStatus, newStatus);
            
            return payment;
        }

        public async Task<bool> DeletePaymentAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var payment = await context.ExpensePayments
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
            if (payment == null)
                return false;

            var invoiceId = payment.InvoiceId;
            
            // Get invoice info before payment is deleted
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            var oldStatus = invoice?.Status;
            var invoiceNumber = invoice?.InvoiceNumber ?? "Unknown";
            
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_payments WHERE id = {0}",
                id);
            
            // Recalculate paid amount and update status
            await RecalculateInvoiceStatusAsync(invoiceId);
            
            // Get new status after recalculation (re-query — ReloadAsync on detached entity silently fails)
            var updatedInvoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            var newStatus = updatedInvoice?.Status;
            
            // Log payment deleted
            await LogAuditAsync(context, invoiceId, invoiceNumber, "PAYMENT_DELETED", 
                $"Amount: {payment.Amount:C}", oldStatus, newStatus);
            
            return true;
        }

        public async Task<ExpensePayment> UpdatePaymentAsync(ExpensePayment payment)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpensePayments
                .AsNoTracking()
                .AnyAsync(p => p.Id == payment.Id);
            if (!exists)
                throw new InvalidOperationException($"Payment with ID {payment.Id} not found");

            var invoiceId = payment.InvoiceId;

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_payments SET
                    payment_date = {0},
                    amount = {1},
                    payment_method = {2},
                    reference = {3},
                    notes = {4}
                WHERE id = {5}",
                payment.PaymentDate,
                payment.Amount,
                payment.PaymentMethod,
                payment.Reference,
                payment.Notes,
                payment.Id);
            
            // Recalculate invoice paid amount and status
            await RecalculateInvoiceStatusAsync(invoiceId);
            
            return payment;
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
            
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_budgets SET planned_amount = {0} WHERE id = {1}",
                budget.PlannedAmount,
                budget.Id);
            
            return budget;
        }

        public async Task<bool> DeleteBudgetAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseBudgets
                .AsNoTracking()
                .AnyAsync(b => b.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_budgets WHERE id = {0}",
                id);
            
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
            
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_categories SET
                    name = {0},
                    code = {1},
                    parent_id = {2},
                    is_active = {3},
                    sort_order = {4}
                WHERE id = {5}",
                category.Name,
                category.Code,
                category.ParentId,
                category.IsActive,
                category.SortOrder,
                category.Id);
            
            return category;
        }

        public async Task<bool> ToggleCategoryActiveAsync(int id, bool isActive)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_categories SET is_active = {0} WHERE id = {1}",
                isActive, id);
            
            return true;
        }

        public async Task<bool> DeleteCategoryAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseCategories
                .AsNoTracking()
                .AnyAsync(c => c.Id == id);
            if (!exists)
                return false;

            // Soft delete: set is_active = false
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_categories SET is_active = {0} WHERE id = {1}",
                false, id);
            
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
            
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_cost_centers SET
                    name = {0},
                    code = {1},
                    is_active = {2}
                WHERE id = {3}",
                center.Name,
                center.Code,
                center.IsActive,
                center.Id);
            
            return center;
        }

        public async Task<bool> DeleteCostCenterAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var exists = await context.ExpenseCostCenters
                .AsNoTracking()
                .AnyAsync(c => c.Id == id);
            if (!exists)
                return false;

            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM expense_cost_centers WHERE id = {0}",
                id);
            
            return true;
        }

        // =====================================================
        // CALCULATIONS
        // =====================================================

        public async Task<ExpenseInvoice?> GetInvoiceAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.ExpenseInvoices.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        }

        public async Task RecalculateInvoiceStatusAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                return;

            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;

            var totalPayments = await context.ExpensePayments
                .Where(p => p.InvoiceId == invoiceId)
                .SumAsync(p => p.Amount);

            // Perduodame currentStatus, kad neprarastume APPROVED žymės
            var newStatus = ExpenseStatusHelper.Recalculate(
                totalPayments,
                invoice.AmountInclVat,
                invoice.DueDate,
                invoice.Status);

            var updatedAt = DateTime.UtcNow;
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    paid_amount = {0},
                    status = {1},
                    updated_at = {2}
                WHERE id = {3}",
                totalPayments, newStatus, updatedAt, invoiceId);
            
            // Log status change if it changed
            if (oldStatus != newStatus)
            {
                await LogAuditAsync(context, invoiceId, invoiceNumber, "STATUS_CHANGED", 
                    $"Status changed from {oldStatus} to {newStatus}", oldStatus, newStatus);
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
                .WhereCountsAsPayable()
                .OrderBy(i => i.DueDate)
                .ToListAsync();
        }

        public async Task RecalculateInvoiceTotalsAsync(int invoiceId)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                return;

            var lines = await context.ExpenseInvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .ToListAsync();

            var amountExclVat = lines.Sum(l => l.AmountExclVat);
            var vatAmount = lines.Sum(l => l.AmountInclVat - l.AmountExclVat);
            var amountInclVat = lines.Sum(l => l.AmountInclVat);
            var updatedAt = DateTime.UtcNow;

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    amount_excl_vat = {0},
                    vat_amount = {1},
                    amount_incl_vat = {2},
                    updated_at = {3}
                WHERE id = {4}",
                amountExclVat, vatAmount, amountInclVat, updatedAt, invoiceId);
        }

        public async Task<BudgetActualsResult> GetBudgetActualsAsync(int year)
        {
            await using var context = _dbFactory.CreateDbContext();
            var start = new DateTime(year, 1, 1);
            var end = start.AddYears(1);

            // Quarantined invoices (DUPLICATE_PENDING / REJECTED) are never actual expenses (D-027)
            var invoices = context.ExpenseInvoices.AsNoTracking()
                .WhereCountsAsPayable()
                .Where(i => i.InvoiceDate >= start && i.InvoiceDate < end);

            // Filtered projections for the year only: lines and their allocations
            var lines = await (
                from i in invoices
                join l in context.ExpenseInvoiceLines on i.Id equals l.InvoiceId
                select new
                {
                    LineId = l.Id,
                    InvoiceId = i.Id,
                    i.InvoiceDate.Month,
                    LineCategoryId = l.CategoryId,
                    InvoiceCategoryId = i.CategoryId,
                    Net = l.AmountExclVat,
                    Gross = (decimal?)l.AmountInclVat ?? 0m
                }).ToListAsync();

            var allocations = await (
                from i in invoices
                join l in context.ExpenseInvoiceLines on i.Id equals l.InvoiceId
                join a in context.ExpenseLineAllocations on l.Id equals a.InvoiceLineId
                select new { a.InvoiceLineId, a.CategoryId, Amount = (decimal?)a.AllocatedAmount ?? 0m }).ToListAsync();
            var allocationsByLine = allocations.ToLookup(a => a.InvoiceLineId);

            // An invoice with no lines contributes its header net under the invoice category — aggregated in SQL
            var linelessTotals = await invoices
                .Where(i => !context.ExpenseInvoiceLines.Any(l => l.InvoiceId == i.Id))
                .GroupBy(i => new { i.CategoryId, i.InvoiceDate.Month })
                .Select(g => new { g.Key.CategoryId, g.Key.Month, Net = g.Sum(i => i.AmountExclVat) })
                .ToListAsync();

            var result = new BudgetActualsResult();
            var totals = new Dictionary<(int? CategoryId, int Month), decimal>();
            void Add(int? categoryId, int month, decimal net)
            {
                if (net == 0m) return;
                totals[(categoryId, month)] = totals.GetValueOrDefault((categoryId, month)) + net;
            }

            foreach (var line in lines)
            {
                var fallbackCategory = line.LineCategoryId ?? line.InvoiceCategoryId;
                var lineAllocations = allocationsByLine[line.LineId].ToList();
                if (lineAllocations.Count == 0)
                {
                    Add(fallbackCategory, line.Month, line.Net);
                    continue;
                }

                // Allocations are entered against the line's gross; convert to net proportionally.
                if (line.Gross == 0m)
                {
                    // Inconsistent data: never invent an amount — contribute 0 and report the line.
                    result.ZeroGrossAllocatedLines.Add(new BudgetAllocationAnomaly(line.InvoiceId, line.LineId));
                    continue;
                }

                var allocated = lineAllocations.Sum(a => a.Amount);
                if (allocated > line.Gross)
                {
                    // Over-allocation: scale the allocations down so they add up to the line's net.
                    result.OverAllocatedLines.Add(new BudgetAllocationAnomaly(line.InvoiceId, line.LineId));
                    foreach (var a in lineAllocations)
                        Add(a.CategoryId, line.Month, line.Net * a.Amount / allocated);
                    continue;
                }

                var ratio = line.Net / line.Gross;
                foreach (var a in lineAllocations)
                    Add(a.CategoryId, line.Month, a.Amount * ratio);
                // The unallocated remainder falls through the D-036 chain, so nothing is lost.
                Add(fallbackCategory, line.Month, (line.Gross - allocated) * ratio);
            }

            foreach (var row in linelessTotals)
                Add(row.CategoryId, row.Month, row.Net);

            foreach (var ((categoryId, month), net) in totals.OrderBy(t => t.Key.Month).ThenBy(t => t.Key.CategoryId))
                result.Rows.Add(new BudgetActualRow(categoryId, month, Math.Round(net, 2)));

            return result;
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

        /// <summary>Invoice-number normalisation for duplicate detection: upper-case, no spaces, '-', '/', '.'.</summary>
        public static string NormalizeInvoiceNumber(string? invoiceNumber) =>
            (invoiceNumber ?? "").ToUpperInvariant().Replace(" ", "").Replace("-", "").Replace("/", "").Replace(".", "");

        /// <summary>
        /// Same normalisation as NormalizeInvoiceNumber, applied to the stored column so it runs in SQL
        /// (UPPER / REPLACE) — never materialise the table to compare in memory.
        /// </summary>
        public static IQueryable<ExpenseInvoice> WhereNormalizedNumberEquals(IQueryable<ExpenseInvoice> query, string normalizedNumber) =>
            query.Where(e => e.InvoiceNumber != null && e.InvoiceNumber != "" &&
                e.InvoiceNumber.ToUpper().Replace(" ", "").Replace("-", "").Replace("/", "").Replace(".", "") == normalizedNumber);

        public async Task<int?> CheckDuplicateAsync(int? supplierId, string? supplierVatCode, string invoiceNumber, decimal amountInclVat, int excludeInvoiceId = 0)
        {
            // D-027 known gap: a zero amount plus a short number ("1" / 0,00) matched different
            // suppliers in production (277 ↔ 173) — never search duplicates on a non-positive amount.
            if (amountInclVat <= 0) return null;
            var normalizedNumber = NormalizeInvoiceNumber(invoiceNumber);
            if (normalizedNumber.Length == 0) return null;
            await using var ctx = _dbFactory.CreateDbContext();

            var query = WhereNormalizedNumberEquals(ctx.ExpenseInvoices, normalizedNumber)
                .Where(e =>
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
            
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return;
            
            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;
            
            // Recalculate OCR flags (remove VENDOR_NOT_FOUND)
            var flags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags ?? "[]") ?? new();
            flags.Remove("VENDOR_NOT_FOUND");
            var ocrFlagsJson = System.Text.Json.JsonSerializer.Serialize(flags);
            var newStatus = StatusAfterSupplierAssigned(flags);

            var now = DateTime.Now;
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    supplier_id = {0},
                    status = {1},
                    updated_at = {2},
                    ocr_flags = {3}
                WHERE id = {4}",
                supplierId, newStatus, now, ocrFlagsJson, invoiceId);

            // Audit log (INSERT — AddAsync + SaveChangesAsync is correct for inserts)
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoiceNumber,
                Action = "SUPPLIER_ASSIGNED", ActionDetails = $"Tiekėjo ID: {supplierId}",
                OldStatus = oldStatus, NewStatus = newStatus,
                PerformedBy = performedBy, PerformedAt = now
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
            var now = DateTime.Now;
            foreach (var invoice in matchingInvoices)
            {
                var oldStatus = invoice.Status;
                var invoiceNumber = invoice.InvoiceNumber;
                
                // Recalculate OCR flags (remove VENDOR_NOT_FOUND)
                var flags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags ?? "[]") ?? new();
                flags.Remove("VENDOR_NOT_FOUND");
                var ocrFlagsJson = System.Text.Json.JsonSerializer.Serialize(flags);
                var newStatus = StatusAfterSupplierAssigned(flags);

                // Build category update if needed
                string? categoryIdSql = null;
                if (supplier?.DefaultExpenseCategoryId.HasValue == true && invoice.CategoryId == null)
                {
                    categoryIdSql = supplier.DefaultExpenseCategoryId.Value.ToString();
                }
                
                if (categoryIdSql != null)
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        UPDATE expense_invoices SET
                            supplier_id = {0},
                            status = {1},
                            updated_at = {2},
                            ocr_flags = {3},
                            category_id = {4}
                        WHERE id = {5}",
                        supplierId, newStatus, now, ocrFlagsJson, categoryIdSql, invoice.Id);
                }
                else
                {
                    await context.Database.ExecuteSqlRawAsync(@"
                        UPDATE expense_invoices SET
                            supplier_id = {0},
                            status = {1},
                            updated_at = {2},
                            ocr_flags = {3}
                        WHERE id = {4}",
                        supplierId, newStatus, now, ocrFlagsJson, invoice.Id);
                }
                
                // Audit log (INSERT — AddAsync + SaveChangesAsync is correct for inserts)
                context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
                {
                    InvoiceId = invoice.Id,
                    InvoiceNumber = invoiceNumber,
                    Action = "SUPPLIER_AUTO_ASSIGNED",
                    ActionDetails = $"Auto-assign: VAT={vatCode}, Name={supplierName}",
                    OldStatus = oldStatus,
                    NewStatus = newStatus,
                    PerformedBy = "SYSTEM",
                    PerformedAt = now
                });
                assignedCount++;
            }

            await context.SaveChangesAsync();
            return assignedCount;
        }

        public async Task ApproveAsync(int invoiceId, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return;
            
            // D-027 §2: a duplicate blocks until it is resolved — never approve it directly
            if (invoice.Status == "DUPLICATE_PENDING")
                throw new InvalidOperationException("Dublikatą pirmiausia reikia išspręsti");

            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;
            var now = DateTime.Now;

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    status = {0},
                    approved_by = {1},
                    approved_at = {2},
                    updated_at = {3}
                WHERE id = {4}",
                "PENDING", performedBy, now, now, invoiceId);
            
            // Audit log (INSERT — AddAsync + SaveChangesAsync is correct for inserts)
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoiceNumber,
                Action = "APPROVED", OldStatus = oldStatus, NewStatus = "PENDING",
                PerformedBy = performedBy, PerformedAt = now
            });
            await context.SaveChangesAsync();
        }

        public async Task ResolveDuplicateAsDifferentAsync(int invoiceId, string performedBy)
        {
            using var context = _dbFactory.CreateDbContext();

            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Sąskaita #{invoiceId} nerasta");

            var flags = ExpenseStatusHelper.ParseFlags(invoice.OcrFlags);
            flags.RemoveAll(f => f == OcrFlag.Duplicate);

            // Only a quarantined invoice gets a new, rule-derived status; an invoice that merely
            // carried the DUPLICATE flag (e.g. after re-OCR) keeps its current status.
            var oldStatus = invoice.Status;
            var newStatus = oldStatus == "DUPLICATE_PENDING" ? DecideOcrStatus(flags, invoice.SupplierId) : oldStatus;
            var rejectedReason = invoice.RejectedReason;
            if (newStatus == "REJECTED" && oldStatus != "REJECTED")
                rejectedReason = $"Sąskaita ne {(await _companySettingsService.GetSettingsAsync()).CompanyName}";

            var ocrFlagsJson = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            var now = DateTime.Now;

            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    ocr_flags = {0},
                    status = {1},
                    rejected_reason = {2},
                    updated_at = {3}
                WHERE id = {4}",
                ocrFlagsJson, newStatus, rejectedReason, now, invoiceId);

            // Audit log — parameterized INSERT
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO expense_invoice_audit
                    (invoice_id, invoice_number, action, action_details, old_status, new_status, performed_by, performed_at)
                VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                invoiceId, invoice.InvoiceNumber, "DUPLICATE_DISMISSED",
                "Pažymėta kaip skirtinga sąskaita (ne dublikatas)",
                oldStatus, newStatus, performedBy, now);
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
            
            var invoice = await context.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null) return;
            
            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;
            var now = DateTime.Now;
            
            await context.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    status = {0},
                    rejected_reason = {1},
                    updated_at = {2}
                WHERE id = {3}",
                "REJECTED", reason, now, invoiceId);
            
            // Audit log (INSERT — AddAsync + SaveChangesAsync is correct for inserts)
            context.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoiceId, InvoiceNumber = invoiceNumber,
                Action = "REJECTED", ActionDetails = reason,
                OldStatus = oldStatus, NewStatus = "REJECTED",
                PerformedBy = performedBy, PerformedAt = now
            });
            await context.SaveChangesAsync();
        }

        // =====================================================
        // STATUS RULES — single source for CreateFromOcrAsync, UpdateFromOcrAsync,
        // AssignSupplierAsync and AutoAssignSupplierAsync, so the review list cannot drift.
        // =====================================================

        /// <summary>
        /// Flags that keep an invoice with a known supplier in NEEDS_REVIEW. The OCR Etapas 1 flags are
        /// classified per D-038: TOTALS_OUT_OF_RANGE, INVALID_IBAN, INVALID_VAT_FORMAT,
        /// VAT_RATE_NOT_ALLOWED, NUMBER_MISREAD and NUMBER_AMBIGUOUS are review;
        /// LINE_AMOUNT_IMPLAUSIBLE, VAT_FORMAT_UNCHECKED and VAT_RATE_UNCHECKED are information only.
        /// </summary>
        private static bool HasReviewFlag(IEnumerable<string> flags) =>
            flags.Any(f => f == OcrFlag.MissingAmount || f == OcrFlag.AmountMismatch ||
                           f == OcrFlag.LowConfidence || f == OcrFlag.ZeroVat ||
                           f == OcrFlag.MissingInvNumber ||
                           f == OcrFlag.AmountArithmeticMismatch || f == OcrFlag.MissingMoneyField ||
                           f == OcrFlag.FutureDate || f == OcrFlag.StaleDate || f == OcrFlag.MissingInvDate ||
                           f == OcrFlag.TotalsOutOfRange || f == OcrFlag.InvalidIban || f == OcrFlag.InvalidVatFormat ||
                           f == OcrFlag.VatRateNotAllowed || f == OcrFlag.NumberMisread || f == OcrFlag.NumberAmbiguous);

        /// <summary>Status precedence for OCR ingestion: WRONG_RECIPIENT → supplier missing → review flags.</summary>
        private static string DecideOcrStatus(IEnumerable<string> flags, int? supplierId)
        {
            var list = flags as ICollection<string> ?? flags.ToList();
            if (list.Contains(OcrFlag.WrongRecipient)) return "REJECTED";
            if (supplierId == null) return "PENDING_SUPPLIER";
            return HasReviewFlag(list) ? "NEEDS_REVIEW" : "PENDING";
        }

        /// <summary>
        /// Status once a supplier is assigned: the review gate still applies (D-028), except that the
        /// document's INVALID_IBAN / INVALID_VAT_FORMAT become information — a human has just
        /// established the supplier's identity (D-038 Q5). The flags stay stored.
        /// </summary>
        private static string StatusAfterSupplierAssigned(IEnumerable<string> flags) =>
            HasReviewFlag(flags.Where(f => f != OcrFlag.InvalidIban && f != OcrFlag.InvalidVatFormat))
                ? "NEEDS_REVIEW" : "PENDING";

        /// <summary>
        /// Drops stale header-arithmetic flags and recomputes them from the final amounts —
        /// the user may have edited amounts after OCR ran.
        /// </summary>
        private static void RecomputeAmountConsistencyFlags(List<string> flags, decimal amountExclVat, decimal vatAmount, decimal amountInclVat)
        {
            flags.RemoveAll(f => f == OcrFlag.AmountArithmeticMismatch || f == OcrFlag.MissingMoneyField);
            var probe = new OcrResultDto { AmountExclVat = amountExclVat, VatAmount = vatAmount, AmountInclVat = amountInclVat };
            ExpenseOcrService.AddAmountConsistencyFlags(probe);
            flags.AddRange(probe.Flags);
        }

        private static DateTime VilniusToday() => LithuanianTimeHelper.ToLithuanianTime(DateTime.UtcNow).Date;

        /// <summary>
        /// Recomputes the invoice-date gates from the final date: MISSING_INV_DATE when the date
        /// could not be parsed (invoiceDate null), FUTURE_DATE when after today (Europe/Vilnius),
        /// STALE_DATE when more than 18 months before the upload date.
        /// </summary>
        private static void RecomputeDateFlags(List<string> flags, DateTime? invoiceDate, DateTime uploadDate)
        {
            flags.RemoveAll(f => f == OcrFlag.FutureDate || f == OcrFlag.StaleDate || f == OcrFlag.MissingInvDate);
            if (invoiceDate == null)
            {
                flags.Add(OcrFlag.MissingInvDate);
                return;
            }
            var date = invoiceDate.Value.Date;
            if (date > VilniusToday()) flags.Add(OcrFlag.FutureDate);
            if (date < uploadDate.Date.AddMonths(-18)) flags.Add(OcrFlag.StaleDate);
        }

        // =====================================================
        // OCR
        // =====================================================

        private static void EnsureInvoiceNumberPresent(OcrResultDto ocrResult)
        {
            if (string.IsNullOrWhiteSpace(ocrResult.InvoiceNumber))
            {
                if (!ocrResult.Flags.Contains(OcrFlag.MissingInvNumber))
                    ocrResult.Flags.Add(OcrFlag.MissingInvNumber);

                throw new InvalidOperationException("Sąskaitos numeris negali būti tuščias. Įveskite numerį rankiniu būdu.");
            }
        }

        public async Task<ExpenseInvoice> CreateFromOcrAsync(OcrResultDto ocrResult, string source = "MANUAL")
        {
            EnsureInvoiceNumberPresent(ocrResult);

            // OCR ingestion can run from an interactive upload (real user) or the background
            // OCR queue worker (no HTTP user context). Distinguish the automated case explicitly
            // instead of masking a missing user as a generic "system" fallback.
            var currentUser = await _authService.GetAuthenticatedUserAsync();
            var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "OCR_PIPELINE";

            DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate);
            var hasInvoiceDate = invoiceDate != default;
            // invoice_date is NOT NULL: keep the placeholder, but flag it (art. 226(1) mandatory field)
            if (!hasInvoiceDate) invoiceDate = DateTime.Today;

            RecomputeAmountConsistencyFlags(ocrResult.Flags, ocrResult.AmountExclVat, ocrResult.VatAmount, ocrResult.AmountInclVat);
            RecomputeDateFlags(ocrResult.Flags, hasInvoiceDate ? invoiceDate : null, VilniusToday());
            var status = DecideOcrStatus(ocrResult.Flags, ocrResult.SupplierId);

            var duplicateId = await CheckDuplicateAsync(ocrResult.SupplierId, ocrResult.SupplierVatCode,
                ocrResult.InvoiceNumber, ocrResult.AmountInclVat);
            if (duplicateId.HasValue)
            {
                if (!ocrResult.Flags.Contains(OcrFlag.Duplicate)) ocrResult.Flags.Add(OcrFlag.Duplicate);
                status = "DUPLICATE_PENDING";
            }

            DateTime.TryParse(ocrResult.DueDate, out var dueDate);
            if (dueDate == default) dueDate = invoiceDate.AddDays(30);

            await using var ctx = _dbFactory.CreateDbContext();

            var companyName = (await _companySettingsService.GetSettingsAsync()).CompanyName;

            // D-010: invoice, lines, audit and the files link are one transaction
            await using var transaction = await ctx.Database.BeginTransactionAsync();

            var invoice = new ExpenseInvoice
            {
                SupplierId = ocrResult.SupplierId,
                InvoiceType = "STANDARD",
                Source = source,
                OriginalFilePath = ocrResult.OriginalFilePath,
                OriginalFilename = ocrResult.OriginalFilename,
                FileId = ocrResult.FileId,
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
                Currency = string.IsNullOrEmpty(ocrResult.Currency) ? NordicBeesERP.Models.PdfLocalization.CurrencyCode : ocrResult.Currency,
                Status = status,
                OcrStatus = "COMPLETED",
                OcrConfidence = ocrResult.Confidence.Overall,
                OcrPipeline = ocrResult.OcrPipeline,
                OcrRawJson = ocrResult.RawJson,
                OcrFlags = ocrResult.Flags.Any() ? System.Text.Json.JsonSerializer.Serialize(ocrResult.Flags) : null,
                SupplierVatVerified = ocrResult.ViesVerified,
                SupplierVatVerifiedName = ocrResult.ViesName,
                RejectedReason = status == "REJECTED" ? $"Sąskaita ne {companyName}" : null,
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

            if (ocrResult.FileId.HasValue)
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE files SET entity_id = {0} WHERE id = {1}",
                    invoice.Id, ocrResult.FileId.Value);
            }

            await transaction.CommitAsync();
            return invoice;
        }

        public async Task<ExpenseInvoice> UpdateFromOcrAsync(int invoiceId, OcrResultDto ocrResult, bool allocationRemovalConfirmed = false)
        {
            EnsureInvoiceNumberPresent(ocrResult);

            // Same rationale as CreateFromOcrAsync: label unattended re-OCR runs explicitly.
            var currentUser = await _authService.GetAuthenticatedUserAsync();
            var performedBy = currentUser?.FullName ?? currentUser?.Email ?? "OCR_PIPELINE";

            await using var ctx = _dbFactory.CreateDbContext();

            var companyNameUpdate = (await _companySettingsService.GetSettingsAsync()).CompanyName;

            // D-010: the invoice UPDATE, the line replacement and the audit row are one transaction
            await using var transaction = await ctx.Database.BeginTransactionAsync();

            var invoice = await ctx.ExpenseInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (invoice == null)
                throw new InvalidOperationException($"Invoice {invoiceId} not found");

            var oldStatus = invoice.Status;
            var invoiceNumber = invoice.InvoiceNumber;

            // A paid invoice is an accounting fact: re-OCR must not overwrite its amounts or status.
            if (oldStatus is "PAID" or "PARTIAL" or "OVERDUE")
                throw new InvalidOperationException("Apmokėtos sąskaitos OCR pakartoti negalima");

            // D-038 Q8: re-OCR replaces the lines, and the FK cascade deletes their allocations.
            // That must be an explicit, confirmed choice, and the removed allocations are audited.
            var removedAllocations = await (
                from a in ctx.ExpenseLineAllocations
                join l in ctx.ExpenseInvoiceLines on a.InvoiceLineId equals l.Id
                where l.InvoiceId == invoiceId
                orderby a.Id
                select new { a.Id, a.InvoiceLineId }).ToListAsync();
            if (removedAllocations.Count > 0 && !allocationRemovalConfirmed)
                throw new InvalidOperationException(
                    $"Sąskaitos eilutės turi paskirstymų ({removedAllocations.Count}). Pakartotinis OCR juos ištrintų – veiksmą reikia patvirtinti.");

            // Quarantined invoices (D-027) keep their status on re-OCR — only data and flags change.
            var keepStatus = oldStatus is "REJECTED" or "DUPLICATE_PENDING";

            // Update invoice fields from OCR result
            DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate);
            var hasInvoiceDate = invoiceDate != default;
            if (!hasInvoiceDate) invoiceDate = DateTime.Today;
            DateTime.TryParse(ocrResult.DueDate, out var dueDate);
            if (dueDate == default) dueDate = invoiceDate.AddDays(30);

            // Determine flags and status
            var flags = new List<string>(ocrResult.Flags);
            var duplicateId = await CheckDuplicateAsync(ocrResult.SupplierId, ocrResult.SupplierVatCode,
                ocrResult.InvoiceNumber, ocrResult.AmountInclVat, excludeInvoiceId: invoiceId);
            if (duplicateId.HasValue)
            {
                if (!flags.Contains(OcrFlag.Duplicate)) flags.Add(OcrFlag.Duplicate);
            }

            RecomputeAmountConsistencyFlags(flags, ocrResult.AmountExclVat, ocrResult.VatAmount, ocrResult.AmountInclVat);
            RecomputeDateFlags(flags, hasInvoiceDate ? invoiceDate : null,
                invoice.CreatedAt != default ? invoice.CreatedAt : VilniusToday());

            string newStatus;
            string? rejectedReason;
            if (keepStatus)
            {
                newStatus = oldStatus;
                rejectedReason = invoice.RejectedReason;
            }
            else
            {
                // Same as CreateFromOcrAsync: a duplicate of another invoice goes to quarantine.
                newStatus = duplicateId.HasValue ? "DUPLICATE_PENDING" : DecideOcrStatus(flags, ocrResult.SupplierId);
                rejectedReason = newStatus == "REJECTED" ? $"Sąskaita ne {companyNameUpdate}" : null;
            }

            var ocrFlagsJson = flags.Any() ? System.Text.Json.JsonSerializer.Serialize(flags) : null;
            var now = DateTime.Now;

            // Single UPDATE for all invoice fields
            await ctx.Database.ExecuteSqlRawAsync(@"
                UPDATE expense_invoices SET
                    supplier_id = {0},
                    pending_supplier_name = {1},
                    pending_supplier_vat = {2},
                    pending_supplier_address = {3},
                    pending_supplier_city = {4},
                    pending_supplier_postal_code = {5},
                    pending_supplier_country_code = {6},
                    pending_supplier_company_code = {7},
                    pending_supplier_bank_account = {8},
                    invoice_number = {9},
                    invoice_date = {10},
                    due_date = {11},
                    amount_excl_vat = {12},
                    vat_rate = {13},
                    vat_amount = {14},
                    amount_incl_vat = {15},
                    currency = {16},
                    ocr_status = {17},
                    ocr_confidence = {18},
                    ocr_pipeline = {19},
                    ocr_raw_json = {20},
                    ocr_flags = {21},
                    supplier_vat_verified = {22},
                    supplier_vat_verified_name = {23},
                    original_file_path = {24},
                    original_filename = {25},
                    status = {26},
                    rejected_reason = {27},
                    updated_at = {28}
                WHERE id = {29}",
                ocrResult.SupplierId,
                ocrResult.SupplierId == null ? ocrResult.SupplierName : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierVatCode : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierAddress : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierCity : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierPostalCode : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierCountryCode : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierCompanyCode : null,
                ocrResult.SupplierId == null ? ocrResult.SupplierBankAccount : null,
                !string.IsNullOrWhiteSpace(ocrResult.InvoiceNumber) ? ocrResult.InvoiceNumber : null,
                invoiceDate,
                dueDate,
                ocrResult.AmountExclVat,
                ocrResult.VatRate,
                ocrResult.VatAmount,
                ocrResult.AmountInclVat,
                string.IsNullOrEmpty(ocrResult.Currency) ? NordicBeesERP.Models.PdfLocalization.CurrencyCode : ocrResult.Currency,
                "COMPLETED",
                ocrResult.Confidence.Overall,
                ocrResult.OcrPipeline,
                ocrResult.RawJson,
                ocrFlagsJson,
                ocrResult.ViesVerified,
                ocrResult.ViesName,
                !string.IsNullOrEmpty(ocrResult.OriginalFilePath) ? ocrResult.OriginalFilePath : invoice.OriginalFilePath,
                // a re-OCR from the file store carries no filename; keep the stored one
                !string.IsNullOrEmpty(ocrResult.OriginalFilename) ? ocrResult.OriginalFilename : invoice.OriginalFilename,
                newStatus,
                rejectedReason,
                now,
                invoiceId);

            // Replace invoice lines — delete old, insert new
            var existingLines = await ctx.ExpenseInvoiceLines
                .Where(l => l.InvoiceId == invoiceId)
                .Select(l => l.Id)
                .ToListAsync();
            if (existingLines.Any())
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "DELETE FROM expense_invoice_lines WHERE invoice_id = {0}",
                    invoiceId);
            }

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

            // Audit log (INSERT — AddAsync + SaveChangesAsync is correct for inserts)
            ctx.ExpenseInvoiceAudits.Add(new ExpenseInvoiceAudit
            {
                InvoiceId = invoice.Id,
                InvoiceNumber = !string.IsNullOrWhiteSpace(ocrResult.InvoiceNumber) ? ocrResult.InvoiceNumber : invoiceNumber,
                Action = "OCR_RETRIED",
                ActionDetails = $"Pakartotinis OCR, tikslumas: {ocrResult.Confidence.Overall}%, požymiai: {string.Join(", ", flags)}"
                    + (keepStatus ? $"; statusas {oldStatus} paliktas (karantinas)" : "")
                    + (removedAllocations.Count > 0
                        ? $"; pašalinta paskirstymų: {removedAllocations.Count} (eilutės: {string.Join(", ", removedAllocations.Select(a => a.InvoiceLineId).Distinct())})"
                        : ""),
                OldStatus = oldStatus,
                NewStatus = newStatus,
                PerformedBy = performedBy,
                PerformedAt = DateTime.Now
            });
            await ctx.SaveChangesAsync();

            await transaction.CommitAsync();
            return invoice;
        }
    }
}
