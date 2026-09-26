using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 0c C2b (D-035): the edit-form save is one transaction, the header is authoritative
/// (never overwritten from line sums), a lines/header mismatch is flagged, and lines are
/// upserted by id so their category allocations survive. Integration tests against
/// nordic_bees_erp_test.
/// </summary>
public class ExpenseEditSaveTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseEditSaveTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"EditSave Supplier {Guid.NewGuid():N}",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task<int> InsertInvoiceAsync(int supplierId, string status = "PENDING", bool zeroVat = false, string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"EDITSAVE-{Guid.NewGuid():N}";
        var (excl, rate, vat) = zeroVat ? (121m, 0m, 0m) : (100m, 21m, 21m);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, approved_by, approved_at, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, NOW(), NOW())",
            number, DateTime.Today, DateTime.Today.AddDays(30), excl, rate, vat, 121m, status, supplierId,
            approvedBy, approvedBy != null ? DateTime.Now : null, "EUR", "MANUAL", "COMPLETED");
        return await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
    }

    private async Task<int> InsertLineAsync(int invoiceId, decimal excl, decimal rate = 21m, string description = "Eilutė", decimal? gross = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"{description} {Guid.NewGuid():N}";
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoice_lines (invoice_id, description, quantity, amount_excl_vat, vat_rate, amount_incl_vat, sort_order) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
            invoiceId, marker, 1m, excl, rate, gross ?? Math.Round(excl * (1 + rate / 100), 2), 1);
        return await context.ExpenseInvoiceLines.Where(l => l.Description == marker).Select(l => l.Id).FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceLine>> LinesAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceLines.AsNoTracking().Where(l => l.InvoiceId == invoiceId).OrderBy(l => l.Id).ToListAsync();
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(int invoiceId, int supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    [Fact]
    public async Task LinelessInvoice_SaveKeepsHeaderAmounts()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.Notes = "Redaguota";
            await CreateService().SaveInvoiceEditAsync(invoice, new List<ExpenseInvoiceLine>(), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal(100m, after.AmountExclVat);
            Assert.Equal(21m, after.VatAmount);
            Assert.Equal(121m, after.AmountInclVat);
            Assert.Equal("Redaguota", after.Notes);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task HeaderDiffersFromLines_AmountMismatch_NeedsReview_HeaderKept()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        await InsertLineAsync(id, 50m);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal(100m, after.AmountExclVat); // never overwritten by the line sum (50)
            Assert.Equal(121m, after.AmountInclVat);
            Assert.Contains(OcrFlag.AmountMismatch, FlagsOf(after));
            Assert.Equal("NEEDS_REVIEW", after.Status);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task HeaderEqualsLines_NoFlag_Pending_Audited()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, status: "NEEDS_REVIEW");
        await InsertLineAsync(id, 60m);
        await InsertLineAsync(id, 40m);
        try
        {
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.DoesNotContain(OcrFlag.AmountMismatch, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var audit = await context.ExpenseInvoiceAudits.AsNoTracking()
                .SingleAsync(a => a.InvoiceId == id && a.Action == "EDITED");
            Assert.Equal("Test User", audit.PerformedBy);
            Assert.Equal("NEEDS_REVIEW", audit.OldStatus);
            Assert.Equal("PENDING", audit.NewStatus);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task FailureMidSave_RollsBack_HeaderAndLinesUnchanged()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        var lineId = await InsertLineAsync(id, 100m);
        try
        {
            var before = await ReloadAsync(id);
            var linesBefore = await LinesAsync(id);

            var edit = await ReloadAsync(id);
            edit.AmountExclVat = 999m;
            edit.Notes = "Neturi išlikti";
            var lines = await LinesAsync(id);
            lines[0].AmountExclVat = 777m;
            // description is NOT NULL: the INSERT of this new line fails after the header UPDATE
            lines.Add(new ExpenseInvoiceLine { Description = null!, Quantity = 1, AmountExclVat = 1m, VatRate = 21m });

            await Assert.ThrowsAnyAsync<Exception>(() => CreateService().SaveInvoiceEditAsync(edit, lines, "Test User"));

            var after = await ReloadAsync(id);
            Assert.Equal(before.AmountExclVat, after.AmountExclVat);
            Assert.Equal(before.Notes, after.Notes);
            Assert.Equal(before.Status, after.Status);
            var linesAfter = await LinesAsync(id);
            Assert.Single(linesAfter);
            Assert.Equal(lineId, linesAfter[0].Id);
            Assert.Equal(linesBefore[0].AmountExclVat, linesAfter[0].AmountExclVat);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task Upsert_KeepsLineIdAndAllocation_DeletesRemovedLine_SavesCategory()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        var keptLineId = await InsertLineAsync(id, 60m);
        var removedLineId = await InsertLineAsync(id, 40m);

        await using var setup = await _fixture.Factory.CreateDbContextAsync();
        var code = "ES" + Guid.NewGuid().ToString("N")[..10];
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_categories (name, code, is_active, sort_order) VALUES ({0}, {1}, 1, 0)", "EditSave kat.", code);
        var categoryId = await setup.ExpenseCategories.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_cost_centers (name, code, is_active) VALUES ({0}, {1}, 1)", "EditSave CC", code);
        var costCenterId = await setup.ExpenseCostCenters.Where(c => c.Code == code).Select(c => c.Id).FirstAsync();
        await setup.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_line_allocations (invoice_line_id, category_id, cost_center_id, allocated_amount, allocated_percent) VALUES ({0}, {1}, {2}, {3}, {4})",
            keptLineId, categoryId, costCenterId, 60m, 100m);
        try
        {
            var edit = await ReloadAsync(id);
            edit.CategoryId = categoryId;
            var lines = (await LinesAsync(id)).Where(l => l.Id == keptLineId).ToList();
            lines[0].Description = "Pakeistas aprašymas";
            lines.Add(new ExpenseInvoiceLine { Description = "Nauja", Quantity = 1, AmountExclVat = 40m, VatRate = 21m });

            await CreateService().SaveInvoiceEditAsync(edit, lines, "Test User");

            var linesAfter = await LinesAsync(id);
            Assert.Equal(2, linesAfter.Count);
            Assert.Contains(linesAfter, l => l.Id == keptLineId && l.Description == "Pakeistas aprašymas");
            Assert.DoesNotContain(linesAfter, l => l.Id == removedLineId);
            Assert.Contains(linesAfter, l => l.Description == "Nauja" && l.AmountInclVat == 48.40m);

            await using var verify = await _fixture.Factory.CreateDbContextAsync();
            Assert.True(await verify.ExpenseLineAllocations.AnyAsync(a => a.InvoiceLineId == keptLineId));
            Assert.Equal(categoryId, (await ReloadAsync(id)).CategoryId);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
            await setup.Database.ExecuteSqlRawAsync("DELETE FROM expense_line_allocations WHERE category_id = {0}", categoryId);
            await setup.Database.ExecuteSqlRawAsync("DELETE FROM expense_cost_centers WHERE id = {0}", costCenterId);
            await setup.Database.ExecuteSqlRawAsync("DELETE FROM expense_categories WHERE id = {0}", categoryId);
        }
    }

    [Fact]
    public async Task ApprovedZeroVat_LineDescriptionOnly_StaysPending()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, zeroVat: true, approvedBy: "Approver");
        await InsertLineAsync(id, 121m, rate: 0m);
        try
        {
            var lines = await LinesAsync(id);
            lines[0].Description = "Tik aprašymas";
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), lines, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Approver", after.ApprovedBy);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task ApprovedZeroVat_OcrLineGrossOffByCent_NotesOnlySave_KeepsGrossAndApproval()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, zeroVat: true, approvedBy: "Approver");
        await InsertLineAsync(id, 121m, rate: 0m, gross: 121.01m); // OCR gross one cent off
        try
        {
            var edit = await ReloadAsync(id);
            edit.Notes = "Tik pastaba";
            await CreateService().SaveInvoiceEditAsync(edit, await LinesAsync(id), "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Approver", after.ApprovedBy);
            Assert.Equal(121.01m, (await LinesAsync(id)).Single().AmountInclVat);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task UnitPrice_SavedForEditedAndNewLines()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        await InsertLineAsync(id, 60m);
        try
        {
            var lines = await LinesAsync(id);
            Assert.Null(lines[0].UnitPrice); // the helper inserts no unit price
            lines[0].Quantity = 4m;
            lines[0].UnitPrice = 15m;
            lines.Add(new ExpenseInvoiceLine { InvoiceId = id, Description = "Nauja", Quantity = 2m, UnitPrice = 20m, AmountExclVat = 40m, VatRate = 21m });

            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), lines, "Test User");

            var after = await LinesAsync(id);
            Assert.Equal(2, after.Count);
            Assert.Equal(15m, after.Single(l => l.Id == lines[0].Id).UnitPrice);
            Assert.Equal(4m, after.Single(l => l.Id == lines[0].Id).Quantity);
            Assert.Equal(20m, after.Single(l => l.Id != lines[0].Id).UnitPrice);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task UnitPrice_UntouchedLine_Kept()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        var lineId = await InsertLineAsync(id, 100m);
        try
        {
            await using (var context = await _fixture.Factory.CreateDbContextAsync())
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE expense_invoice_lines SET unit_price = {0} WHERE id = {1}", 12.5m, lineId);

            var invoice = await ReloadAsync(id);
            invoice.Notes = "Tik pastaba";
            await CreateService().SaveInvoiceEditAsync(invoice, await LinesAsync(id), "Test User");

            Assert.Equal(12.5m, (await LinesAsync(id)).Single().UnitPrice);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    [Fact]
    public async Task LineOfAnotherInvoice_Refused_NothingWritten()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId);
        var otherId = await InsertInvoiceAsync(supplierId);
        var foreignLineId = await InsertLineAsync(otherId, 10m);
        try
        {
            var edit = await ReloadAsync(id);
            edit.Notes = "Neturi išlikti";
            var foreign = (await LinesAsync(otherId)).Single();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateService().SaveInvoiceEditAsync(edit, new List<ExpenseInvoiceLine> { foreign }, "Test User"));
            Assert.Equal("Eilutė nepriklauso šiai sąskaitai", ex.Message);

            Assert.Null((await ReloadAsync(id)).Notes);
            Assert.Equal(otherId, (await LinesAsync(otherId)).Single(l => l.Id == foreignLineId).InvoiceId);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
            await using var context = await _fixture.Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", otherId);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", otherId);
        }
    }

    [Fact]
    public async Task ApprovedZeroVat_LineAmountChanged_NeedsReview_ApprovalVoided()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(supplierId, zeroVat: true, approvedBy: "Approver");
        await InsertLineAsync(id, 121m, rate: 0m);
        try
        {
            var lines = await LinesAsync(id);
            lines[0].AmountExclVat = 100m;
            await CreateService().SaveInvoiceEditAsync(await ReloadAsync(id), lines, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Null(after.ApprovedBy);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var voided = await context.ExpenseInvoiceAudits.AsNoTracking()
                .SingleAsync(a => a.InvoiceId == id && a.Action == "APPROVAL_VOIDED");
            Assert.Contains("lines", voided.ActionDetails);
            Assert.Equal("Test User", voided.PerformedBy);
        }
        finally
        {
            await CleanupAsync(id, supplierId);
        }
    }

    // ============ Stub implementations ============

    private sealed class NullAuthService : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class DefaultCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
