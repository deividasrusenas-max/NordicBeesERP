using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 S1 (PLAN-ETAPAS2 §3, D-044): gate 3 — an invoice cannot leave PENDING_SUPPLIER without a
/// supplier (approve refused, restore by the shared rules, assign only from PENDING_SUPPLIER), and re-OCR
/// never unassigns or swaps a supplier a human assigned (VENDOR_SUGGESTED). Integration tests against
/// nordic_bees_erp_test.
/// </summary>
public class ExpenseSupplierGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseSupplierGateTests(DbTestFixture fixture)
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
            Name = $"S1Gate Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string>? flags = null,
        string? number = null, decimal incl = 121m, string? rejectedReason = null, string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        number ??= $"S1GATE-{Guid.NewGuid():N}";
        var marker = $"S1GATE-MARK-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        var excl = Math.Round(incl * 100m / 121m, 2);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, rejected_reason, approved_by, notes, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, NOW(), NOW())",
            number, date, date.AddDays(30), excl, 21m, incl - excl, incl, status, supplierId,
            JsonSerializer.Serialize(flags ?? Array.Empty<string>()), rejectedReason, approvedBy, marker,
            "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
    }

    private static OcrResultDto NewOcrResult(int? supplierId, IEnumerable<string>? flags = null) => new()
    {
        InvoiceNumber = $"S1GATE-OCR-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = 121m,
        SupplierId = supplierId,
        SupplierName = "S1Gate OCR Supplier",
        Flags = flags?.ToList() ?? new List<string>(),
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<List<ExpenseInvoiceAudit>> AuditsAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().Where(a => a.InvoiceId == id).ToListAsync();
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(IEnumerable<int> invoiceIds, params int[] supplierIds)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoiceIds)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_lines WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        foreach (var supplierId in supplierIds)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId);
    }

    // ---------------------------------------------------------------- (a) approve

    [Fact]
    public async Task Approve_PendingSupplierWithoutSupplier_Refused_NothingWritten()
    {
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound });
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ApproveAsync(id, "Test User"));
            Assert.Equal("Pirmiausia priskirkite tiekėją", ex.Message);

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING_SUPPLIER", after.Status);
            Assert.Null(after.ApprovedBy);
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(new[] { id });
        }
    }

    [Fact]
    public async Task Approve_WithSupplier_StillWorks()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, new[] { OcrFlag.ZeroVat });
        try
        {
            await CreateService().ApproveAsync(id, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Test User", after.ApprovedBy);
            Assert.Single(await AuditsAsync(id), a => a.Action == "APPROVED");
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    // ---------------------------------------------------------------- (b) restore

    [Fact]
    public async Task Restore_RejectedWithoutSupplier_GoesToPendingSupplier_ReasonCleared_Audited()
    {
        var id = await InsertInvoiceAsync("REJECTED", null, new[] { OcrFlag.VendorNotFound }, rejectedReason: "Neteisinga sąskaita");
        try
        {
            await CreateService().RestoreInvoiceAsync(id, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING_SUPPLIER", after.Status);
            Assert.Null(after.RejectedReason);

            var audit = Assert.Single(await AuditsAsync(id));
            Assert.Equal("RESTORED", audit.Action);
            Assert.Equal("REJECTED", audit.OldStatus);
            Assert.Equal("PENDING_SUPPLIER", audit.NewStatus);
            Assert.Equal("Test User", audit.PerformedBy);
        }
        finally
        {
            await CleanupAsync(new[] { id });
        }
    }

    [Fact]
    public async Task Restore_WrongRecipient_FlagRemoved_Audited_NotReRejected()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("REJECTED", supplierId, new[] { OcrFlag.WrongRecipient, OcrFlag.LinesNotFound },
            rejectedReason: "Sąskaita ne MB Lakštenai");
        try
        {
            await CreateService().RestoreInvoiceAsync(id, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.NotEqual("REJECTED", after.Status);
            Assert.Null(after.RejectedReason);
            Assert.DoesNotContain(OcrFlag.WrongRecipient, FlagsOf(after));
            Assert.Contains(OcrFlag.LinesNotFound, FlagsOf(after));

            var audits = await AuditsAsync(id);
            var dismissed = Assert.Single(audits, a => a.Action == "WRONG_RECIPIENT_DISMISSED");
            Assert.Equal("Test User", dismissed.PerformedBy);
            Assert.Equal("REJECTED", dismissed.OldStatus);
            Assert.Equal("PENDING", dismissed.NewStatus);
            Assert.Single(audits, a => a.Action == "RESTORED");
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Restore_ReviewFlagRemains_GoesToNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("REJECTED", supplierId, new[] { OcrFlag.ZeroVat });
        try
        {
            await CreateService().RestoreInvoiceAsync(id, "Test User");

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Restore_DuplicateOfAnotherInvoice_GoesBackToQuarantine_NotPayable()
    {
        var supplierId = await InsertSupplierAsync();
        var number = $"S1GATE-DUP-{Guid.NewGuid():N}";
        var original = await InsertInvoiceAsync("PENDING", supplierId, number: number, incl: 121m);
        var rejected = await InsertInvoiceAsync("REJECTED", supplierId, number: number, incl: 121m, rejectedReason: "Dublikatas");
        try
        {
            await CreateService().RestoreInvoiceAsync(rejected, "Test User");

            var after = await ReloadAsync(rejected);
            Assert.Equal("DUPLICATE_PENDING", after.Status);
            Assert.Contains(OcrFlag.Duplicate, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { original, rejected }, supplierId);
        }
    }

    [Theory]
    [InlineData("PENDING")]
    [InlineData("PAID")]
    [InlineData("PENDING_SUPPLIER")]
    public async Task Restore_NotRejected_Refused_NothingWritten(string status)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(status, status == "PENDING_SUPPLIER" ? null : supplierId);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().RestoreInvoiceAsync(id, "Test User"));

            Assert.Equal(status, (await ReloadAsync(id)).Status);
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    // ---------------------------------------------------------------- (c) assign

    [Theory]
    [InlineData("DUPLICATE_PENDING")]
    [InlineData("REJECTED")]
    [InlineData("PAID")]
    [InlineData("NEEDS_REVIEW")]
    public async Task Assign_NotPendingSupplier_Refused_StatusAndSupplierUnchanged(string status)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(status, null, new[] { OcrFlag.Duplicate });
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().AssignSupplierAsync(id, supplierId, "Test User"));
            Assert.Equal("Tiekėją galima priskirti tik sąskaitai, kuri laukia tiekėjo", ex.Message);

            var after = await ReloadAsync(id);
            Assert.Equal(status, after.Status);
            Assert.Null(after.SupplierId);
            Assert.Empty(await AuditsAsync(id));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Assign_PendingSupplier_StillWorks_AndClearsSuggestionFlag()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound, OcrFlag.VendorSuggested });
        try
        {
            await CreateService().AssignSupplierAsync(id, supplierId, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal(supplierId, after.SupplierId);
            Assert.Equal("PENDING", after.Status);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, FlagsOf(after));
            Assert.DoesNotContain(OcrFlag.VendorSuggested, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    // ---------------------------------------------------------------- (d) re-OCR

    [Fact]
    public async Task ReOcr_FreshMatchNull_KeepsAssignedSupplier_NoFlag()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(null, new[] { OcrFlag.VendorNotFound }));

            var after = await ReloadAsync(id);
            Assert.Equal(supplierId, after.SupplierId);
            Assert.NotEqual("PENDING_SUPPLIER", after.Status);
            Assert.DoesNotContain(OcrFlag.VendorNotFound, FlagsOf(after));
            Assert.DoesNotContain(OcrFlag.VendorSuggested, FlagsOf(after));
            Assert.DoesNotContain(await AuditsAsync(id), a => a.Action == "SUPPLIER_SUGGESTED");
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task ReOcr_FreshMatchIsDifferentPartner_KeepsAssigned_AddsSuggestion_InformationOnly()
    {
        var assigned = await InsertSupplierAsync();
        var other = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", assigned);
        var control = await InsertInvoiceAsync("PENDING", assigned);
        try
        {
            var service = CreateService();
            await service.UpdateFromOcrAsync(control, NewOcrResult(assigned));
            await service.UpdateFromOcrAsync(id, NewOcrResult(other));

            var after = await ReloadAsync(id);
            Assert.Equal(assigned, after.SupplierId);
            Assert.Contains(OcrFlag.VendorSuggested, FlagsOf(after));
            Assert.DoesNotContain(OcrFlag.VendorNotFound, FlagsOf(after));

            // information only: same status as the control re-OCR that found the assigned partner itself
            var controlAfter = await ReloadAsync(control);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, FlagsOf(controlAfter));
            Assert.Equal(controlAfter.Status, after.Status);
            Assert.False(ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VendorSuggested));

            var audit = Assert.Single(await AuditsAsync(id), a => a.Action == "SUPPLIER_SUGGESTED");
            Assert.StartsWith($"partner_id={other};", audit.ActionDetails);
            Assert.Equal(other, await service.GetSuggestedSupplierIdAsync(id));
            Assert.Null(await service.GetSuggestedSupplierIdAsync(control));
        }
        finally
        {
            await CleanupAsync(new[] { id, control }, assigned, other);
        }
    }

    [Fact]
    public async Task ReOcr_FreshMatchIsSamePartner_NoSuggestion()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId);
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId));

            var after = await ReloadAsync(id);
            Assert.Equal(supplierId, after.SupplierId);
            Assert.DoesNotContain(OcrFlag.VendorSuggested, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task ReOcr_NoAssignedSupplier_FreshMatchIsUsed_AsBefore()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound });
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(supplierId));

            var after = await ReloadAsync(id);
            Assert.Equal(supplierId, after.SupplierId);
            Assert.NotEqual("PENDING_SUPPLIER", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task ReOcr_NoAssignedSupplier_FreshMatchNull_StaysPendingSupplier()
    {
        var id = await InsertInvoiceAsync("PENDING_SUPPLIER", null, new[] { OcrFlag.VendorNotFound });
        try
        {
            await CreateService().UpdateFromOcrAsync(id, NewOcrResult(null, new[] { OcrFlag.VendorNotFound }));

            var after = await ReloadAsync(id);
            Assert.Null(after.SupplierId);
            Assert.Equal("PENDING_SUPPLIER", after.Status);
            Assert.Contains(OcrFlag.VendorNotFound, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id });
        }
    }

    // ---------------------------------------------------------------- approved invoice keeps its supplier

    [Fact]
    public async Task ApprovedInvoice_KeepsSupplier_ThroughReOcrAndAssign()
    {
        var supplierId = await InsertSupplierAsync();
        var other = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, approvedBy: "Buhalterė");
        try
        {
            var service = CreateService();

            await service.UpdateFromOcrAsync(id, NewOcrResult(null, new[] { OcrFlag.VendorNotFound }));
            Assert.Equal(supplierId, (await ReloadAsync(id)).SupplierId);

            await service.UpdateFromOcrAsync(id, NewOcrResult(other));
            Assert.Equal(supplierId, (await ReloadAsync(id)).SupplierId);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignSupplierAsync(id, other, "Test User"));
            var after = await ReloadAsync(id);
            Assert.Equal(supplierId, after.SupplierId);
            Assert.Equal("Buhalterė", after.ApprovedBy);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId, other);
        }
    }

    // ---------------------------------------------------------------- VENDOR_SUGGESTED plumbing

    [Fact]
    public async Task VendorSuggested_IsCarriedOverByTheEditPath_AndIsNotAReviewFlag()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.VendorSuggested });
        try
        {
            var invoice = await ReloadAsync(id);
            invoice.Notes = "edit";
            await CreateService().UpdateInvoiceAsync(invoice);

            var after = await ReloadAsync(id);
            Assert.Contains(OcrFlag.VendorSuggested, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public void VendorSuggested_HasLithuanianLabel_AndIsNotCritical()
    {
        Assert.Equal("Siūlomas kitas tiekėjas", ExpenseStatusHelper.GetFlagLabel(OcrFlag.VendorSuggested));
        Assert.False(ExpenseStatusHelper.IsCriticalFlag(OcrFlag.VendorSuggested));
    }

    [Theory]
    [InlineData("partner_id=42; pakartotinis OCR", 42)]
    [InlineData("partner_id=7", 7)]
    [InlineData("partner_id=; x", null)]
    [InlineData("Tiekėjo ID: 5", null)]
    [InlineData(null, null)]
    public void ParseSuggestedPartnerId_ReadsOnlyTheAuditPrefix(string? details, int? expected)
    {
        Assert.Equal(expected, ExpenseService.ParseSuggestedPartnerId(details));
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
