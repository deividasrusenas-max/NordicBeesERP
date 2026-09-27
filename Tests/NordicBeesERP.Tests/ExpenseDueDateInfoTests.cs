using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-025 (B2): MISSING_DUE_DATE is information, not an error. The label says the due date
/// was assumed (+30 d.), the colour stays Default, and the flag does not change status.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseDueDateInfoTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseDueDateInfoTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Label_SaysAssumed_ColourStaysDefault()
    {
        Assert.Equal("Terminas numatytas (+30 d.)", ExpenseStatusHelper.GetFlagLabel(OcrFlag.MissingDueDate));
        Assert.Equal(Color.Default, ExpenseStatusHelper.GetFlagColor(OcrFlag.MissingDueDate));
    }

    [Theory]
    [InlineData("[\"MISSING_DUE_DATE\"]", true)]
    [InlineData("[\"LINES_NOT_FOUND\",\"MISSING_DUE_DATE\"]", true)]
    [InlineData("[\"LINES_NOT_FOUND\"]", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsDueDateAssumed_ReadsFlag(string? ocrFlags, bool expected)
    {
        Assert.Equal(expected, ExpenseStatusHelper.IsDueDateAssumed(ocrFlags));
    }

    [Fact]
    public async Task MissingDueDate_DoesNotChangeStatus_DueDateDefaultsToPlus30()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = $"DueInfo Supplier {Guid.NewGuid():N}",
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

        int? invoiceId = null;
        try
        {
            var invoiceDate = LithuanianTimeHelper.ToLithuanianTime(DateTime.UtcNow).Date;
            var ocr = new OcrResultDto
            {
                InvoiceNumber = $"DUE-INFO-{Guid.NewGuid():N}",
                InvoiceDate = invoiceDate.ToString("yyyy-MM-dd"),
                DueDate = "",
                AmountExclVat = 100m,
                VatRate = 21m,
                VatAmount = 21m,
                AmountInclVat = 121m,
                SupplierId = partner.Id,
                SupplierName = partner.Name,
                Flags = new List<string> { OcrFlag.MissingDueDate },
                Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
            };

            var service = new ExpenseService(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService());
            var created = await service.CreateFromOcrAsync(ocr);
            invoiceId = created.Id;

            var reloaded = await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == created.Id);
            Assert.Equal("PENDING", reloaded.Status);
            Assert.Equal(invoiceDate.AddDays(30), reloaded.DueDate.Date);
            Assert.True(ExpenseStatusHelper.IsDueDateAssumed(reloaded.OcrFlags));
        }
        finally
        {
            if (invoiceId.HasValue)
            {
                await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoiceId.Value);
                await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoiceId.Value);
            }
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partner.Id);
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
