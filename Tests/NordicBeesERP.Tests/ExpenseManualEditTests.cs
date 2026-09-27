using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Etapas 0c C2: manual edit (UpdateInvoiceAsync) uses the same review rules as OCR ingestion,
/// reads the old status from the DB, and wrong-recipient dismissal is its own audited method.
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseManualEditTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseManualEditTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly DateTime Today = DateTime.Today;

    private ExpenseService CreateService() =>
        new(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());

    private async Task<int> InsertSupplierAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"ManualEdit Supplier {Guid.NewGuid():N}",
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

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, IEnumerable<string> flags,
        string? rejectedReason = null, bool zeroVat = false, string? approvedBy = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var number = $"MEDIT-{Guid.NewGuid():N}";
        var (excl, rate, vat) = zeroVat ? (121m, 0m, 0m) : (100m, 21m, 21m);

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, rejected_reason, approved_by, approved_at, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, {15}, NOW(), NOW())",
            number, Today, Today.AddDays(30), excl, rate, vat, 121m, status, supplierId,
            JsonSerializer.Serialize(flags), rejectedReason, approvedBy, approvedBy != null ? DateTime.Now : null,
            "EUR", "MANUAL", "COMPLETED");

        return await context.ExpenseInvoices.Where(i => i.InvoiceNumber == number).Select(i => i.Id).FirstAsync();
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private static List<string> FlagsOf(ExpenseInvoice invoice) =>
        string.IsNullOrEmpty(invoice.OcrFlags) ? new() : JsonSerializer.Deserialize<List<string>>(invoice.OcrFlags) ?? new();

    private async Task CleanupAsync(IEnumerable<int> invoiceIds, int? supplierId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in invoiceIds)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
        if (supplierId.HasValue)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", supplierId.Value);
    }

    /// <summary>The edit form's object: stored values with optional overrides.</summary>
    private static ExpenseInvoice Edit(ExpenseInvoice s, decimal? excl = null, decimal? vat = null, decimal? incl = null,
        decimal? vatRate = null, DateTime? due = null, string? callerStatus = null) => new()
    {
        Id = s.Id,
        SupplierId = s.SupplierId,
        InvoiceNumber = s.InvoiceNumber,
        InvoiceDate = s.InvoiceDate,
        DueDate = due ?? s.DueDate,
        AmountExclVat = excl ?? s.AmountExclVat,
        VatRate = vatRate ?? s.VatRate,
        VatAmount = vat ?? s.VatAmount,
        AmountInclVat = incl ?? s.AmountInclVat,
        Notes = s.Notes,
        Status = callerStatus ?? s.Status,
        OcrFlags = s.OcrFlags
    };

    [Fact]
    public async Task Pending_EditBreaksAmounts_MovesToNeedsReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, Array.Empty<string>());
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id), excl: 803.31m, vat: 168.69m, incl: 1000m));

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task Pending_ZeroVat_StaysReviewFlag()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, Array.Empty<string>());
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id), excl: 121m, vat: 0m, incl: 121m, vatRate: 0m));

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Contains(OcrFlag.ZeroVat, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task NoSupplier_GoesToPendingSupplier()
    {
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", null, new[] { OcrFlag.VendorNotFound, OcrFlag.MissingMoneyField });
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id)));

            Assert.Equal("PENDING_SUPPLIER", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, null);
        }
    }

    [Theory]
    [InlineData("PAID")]
    [InlineData("PARTIAL")]
    [InlineData("OVERDUE")]
    [InlineData("REJECTED")]
    [InlineData("DUPLICATE_PENDING")]
    public async Task ClosedOrQuarantined_StatusKept_FlagStored(string status)
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync(status, supplierId, Array.Empty<string>());
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id), excl: 803.31m, vat: 168.69m, incl: 1000m));

            var after = await ReloadAsync(id);
            Assert.Equal(status, after.Status);
            Assert.Contains(OcrFlag.AmountArithmeticMismatch, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Theory]
    [InlineData("PAID", "[\"AMOUNT_ARITHMETIC_MISMATCH\"]", true)]
    [InlineData("PAID", "[\"MISSING_MONEY_FIELD\"]", true)]
    [InlineData("PAID", "[\"FUTURE_DATE\"]", true)]
    [InlineData("PAID", "[\"STALE_DATE\"]", true)]
    [InlineData("PAID", "[\"MISSING_INV_DATE\"]", true)]
    [InlineData("PAID", "[\"MISSING_DUE_DATE\",\"LINES_NOT_FOUND\"]", false)]
    [InlineData("PAID", null, false)]
    [InlineData("PARTIAL", "[\"AMOUNT_ARITHMETIC_MISMATCH\"]", true)]
    [InlineData("REJECTED", "[\"AMOUNT_ARITHMETIC_MISMATCH\"]", false)]
    public void NeedsAttention_HighlightsGateFlags_IncludingPaid(string status, string? flags, bool expected)
    {
        Assert.Equal(expected, ExpenseStatusHelper.NeedsAttention(status, flags));
    }

    [Fact]
    public async Task ApprovedZeroVat_NotesOnlyEdit_StaysPending_ApprovalKept()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.ZeroVat }, zeroVat: true, approvedBy: "Approver");
        try
        {
            var edit = Edit(await ReloadAsync(id));
            edit.Notes = "Tik pastaba";
            await CreateService().UpdateInvoiceAsync(edit);

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Equal("Approver", after.ApprovedBy);
            Assert.Contains(OcrFlag.ZeroVat, FlagsOf(after)); // flag still recorded
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task ApprovedZeroVat_AmountEdit_NeedsReview_ApprovalVoidedAndAudited()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.ZeroVat }, zeroVat: true, approvedBy: "Approver");
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id), excl: 150m, incl: 150m));

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Null(after.ApprovedBy);
            Assert.Null(after.ApprovedAt);

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var audit = await context.ExpenseInvoiceAudits.AsNoTracking()
                .SingleAsync(a => a.InvoiceId == id && a.Action == "APPROVAL_VOIDED");
            Assert.Contains("amount_excl_vat", audit.ActionDetails);
            Assert.Contains("amount_incl_vat", audit.ActionDetails);
            Assert.Equal("PENDING", audit.OldStatus);
            Assert.Equal("NEEDS_REVIEW", audit.NewStatus);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task UnapprovedZeroVat_NotesOnlyEdit_FollowsNormalRules()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, Array.Empty<string>(), zeroVat: true);
        try
        {
            var edit = Edit(await ReloadAsync(id));
            edit.Notes = "Tik pastaba";
            await CreateService().UpdateInvoiceAsync(edit);

            Assert.Equal("NEEDS_REVIEW", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task CallerSentStatus_IsIgnored()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, Array.Empty<string>());
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id), callerStatus: "PAID"));

            Assert.Equal("PENDING", (await ReloadAsync(id)).Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task MissingDueDate_KeptWhenDueDateUnchanged_DroppedWhenChanged()
    {
        var supplierId = await InsertSupplierAsync();
        var kept = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.MissingDueDate });
        var changed = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.MissingDueDate });
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(kept)));
            Assert.Contains(OcrFlag.MissingDueDate, FlagsOf(await ReloadAsync(kept)));

            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(changed), due: Today.AddDays(45)));
            Assert.DoesNotContain(OcrFlag.MissingDueDate, FlagsOf(await ReloadAsync(changed)));
        }
        finally
        {
            await CleanupAsync(new[] { kept, changed }, supplierId);
        }
    }

    [Fact]
    public async Task LowConfidence_DroppedOnManualSave_ReleasesReview()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("NEEDS_REVIEW", supplierId, new[] { OcrFlag.LowConfidence });
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id)));

            var after = await ReloadAsync(id);
            Assert.DoesNotContain(OcrFlag.LowConfidence, FlagsOf(after));
            Assert.Equal("PENDING", after.Status);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task WrongRecipient_HoldsInReview_OnEdit()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("PENDING", supplierId, new[] { OcrFlag.WrongRecipient });
        try
        {
            await CreateService().UpdateInvoiceAsync(Edit(await ReloadAsync(id)));

            var after = await ReloadAsync(id);
            Assert.Equal("NEEDS_REVIEW", after.Status);
            Assert.Contains(OcrFlag.WrongRecipient, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task DismissWrongRecipient_RejectedForRecipient_ReleasedAndAudited()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("REJECTED", supplierId, new[] { OcrFlag.WrongRecipient }, "Sąskaita ne MB Test");
        try
        {
            await CreateService().DismissWrongRecipientAsync(id, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("PENDING", after.Status);
            Assert.Null(after.RejectedReason);
            Assert.DoesNotContain(OcrFlag.WrongRecipient, FlagsOf(after));

            await using var context = await _fixture.Factory.CreateDbContextAsync();
            var audit = await context.ExpenseInvoiceAudits.AsNoTracking()
                .SingleAsync(a => a.InvoiceId == id && a.Action == "WRONG_RECIPIENT_DISMISSED");
            Assert.Equal("Test User", audit.PerformedBy);
            Assert.Equal("REJECTED", audit.OldStatus);
            Assert.Equal("PENDING", audit.NewStatus);
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
        }
    }

    [Fact]
    public async Task DismissWrongRecipient_RejectedForOtherReason_StaysRejected()
    {
        var supplierId = await InsertSupplierAsync();
        var id = await InsertInvoiceAsync("REJECTED", supplierId, new[] { OcrFlag.WrongRecipient }, "Dublikatas: X-1");
        try
        {
            await CreateService().DismissWrongRecipientAsync(id, "Test User");

            var after = await ReloadAsync(id);
            Assert.Equal("REJECTED", after.Status);
            Assert.Equal("Dublikatas: X-1", after.RejectedReason);
            Assert.DoesNotContain(OcrFlag.WrongRecipient, FlagsOf(after));
        }
        finally
        {
            await CleanupAsync(new[] { id }, supplierId);
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
