using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// PAY-02: expense payments validate on the server, refuse an immediate duplicate and keep the
/// invoice status in step; PaymentService reports Lithuanian errors that name the invoice number.
/// Integration tests against nordic_bees_erp_test, fresh contexts for every assertion.
/// </summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class ExpensePaymentIntegrityTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpensePaymentIntegrityTests(DbTestFixture fixture) => _fixture = fixture;

    private ExpenseService NewService() =>
        new(_fixture.Factory, new NullAuth(), new DefaultSettings());

    private async Task<int> SeedExpenseInvoiceAsync(List<int> ids, decimal total)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var invoice = new ExpenseInvoice
        {
            InvoiceNumber = $"PAY02-{Guid.NewGuid():N}",
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(30),
            AmountExclVat = total,
            VatAmount = 0m,
            AmountInclVat = total,
            Status = "APPROVED",
            OcrStatus = "PENDING",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.ExpenseInvoices.Add(invoice);
        await ctx.SaveChangesAsync();
        ids.Add(invoice.Id);
        return invoice.Id;
    }

    private async Task CleanupAsync(List<int> ids)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in ids)
        {
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM expense_payments WHERE invoice_id = {0}", id);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", id);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", id);
        }
    }

    private static ExpensePayment NewPayment(int invoiceId, decimal amount, DateTime date) => new()
    {
        InvoiceId = invoiceId, Amount = amount, PaymentDate = date, PaymentMethod = "BANK", Source = "manual"
    };

    private async Task<(decimal Paid, string Status, int Count)> ReadAsync(int invoiceId)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var inv = await ctx.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId);
        var count = await ctx.ExpensePayments.AsNoTracking().CountAsync(p => p.InvoiceId == invoiceId);
        return (inv.PaidAmount, inv.Status, count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task AddPayment_NonPositiveAmount_ThrowsAndWritesNothing(int amount)
    {
        var ids = new List<int>();
        try
        {
            var invoiceId = await SeedExpenseInvoiceAsync(ids, 100m);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                NewService().AddPaymentAsync(NewPayment(invoiceId, amount, DateTime.UtcNow.Date)));
            Assert.Equal("Mokėjimo suma turi būti didesnė už 0.", ex.Message);
            Assert.Equal((0m, "APPROVED", 0), await ReadAsync(invoiceId));
        }
        finally { await CleanupAsync(ids); }
    }

    [Fact]
    public async Task AddPayment_SameAmountAndDateTwiceInARow_SecondRefused_OnePaymentRecorded()
    {
        var ids = new List<int>();
        try
        {
            var invoiceId = await SeedExpenseInvoiceAsync(ids, 100m);
            var service = NewService();
            await service.AddPaymentAsync(NewPayment(invoiceId, 40m, DateTime.UtcNow.Date));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AddPaymentAsync(NewPayment(invoiceId, 40m, DateTime.UtcNow.Date)));
            Assert.Equal("Toks mokėjimas ką tik užregistruotas – patikrinkite mokėjimų sąrašą.", ex.Message);

            var (paid, _, count) = await ReadAsync(invoiceId);
            Assert.Equal(40m, paid);
            Assert.Equal(1, count);
        }
        finally { await CleanupAsync(ids); }
    }

    [Fact]
    public async Task AddPayment_DifferentAmount_Accepted_StatusAndPaidAmountFollow()
    {
        var ids = new List<int>();
        try
        {
            var invoiceId = await SeedExpenseInvoiceAsync(ids, 100m);
            var service = NewService();
            await service.AddPaymentAsync(NewPayment(invoiceId, 40m, DateTime.UtcNow.Date));
            await service.AddPaymentAsync(NewPayment(invoiceId, 60m, DateTime.UtcNow.Date));

            var (paid, status, count) = await ReadAsync(invoiceId);
            Assert.Equal(100m, paid);
            Assert.Equal("PAID", status);
            Assert.Equal(2, count);
        }
        finally { await CleanupAsync(ids); }
    }

    [Fact]
    public async Task UpdatePayment_NonPositiveAmount_Throws_PaymentUnchanged()
    {
        var ids = new List<int>();
        try
        {
            var invoiceId = await SeedExpenseInvoiceAsync(ids, 100m);
            var service = NewService();
            var added = await service.AddPaymentAsync(NewPayment(invoiceId, 40m, DateTime.UtcNow.Date));

            var edit = NewPayment(invoiceId, 0m, DateTime.UtcNow.Date);
            edit.Id = added.Id;
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePaymentAsync(edit));
            Assert.Equal("Mokėjimo suma turi būti didesnė už 0.", ex.Message);
            Assert.Equal(40m, (await ReadAsync(invoiceId)).Paid);
        }
        finally { await CleanupAsync(ids); }
    }

    [Fact]
    public async Task UpdateAndDeletePayment_RecalculateInvoice()
    {
        var ids = new List<int>();
        try
        {
            var invoiceId = await SeedExpenseInvoiceAsync(ids, 100m);
            var service = NewService();
            var added = await service.AddPaymentAsync(NewPayment(invoiceId, 40m, DateTime.UtcNow.Date));

            var edit = NewPayment(invoiceId, 100m, DateTime.UtcNow.Date);
            edit.Id = added.Id;
            await service.UpdatePaymentAsync(edit);
            var afterUpdate = await ReadAsync(invoiceId);
            Assert.Equal(100m, afterUpdate.Paid);
            Assert.Equal("PAID", afterUpdate.Status);

            Assert.True(await service.DeletePaymentAsync(added.Id));
            var afterDelete = await ReadAsync(invoiceId);
            Assert.Equal(0m, afterDelete.Paid);
            Assert.Equal(0, afterDelete.Count);
            Assert.NotEqual("PAID", afterDelete.Status);
        }
        finally { await CleanupAsync(ids); }
    }

    // ---- PaymentService Lithuanian messages ----

    [Fact]
    public async Task RegisterPayment_UnknownInvoice_LithuanianMessage()
    {
        var service = new PaymentService(_fixture.Factory);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterPaymentAsync(new List<int> { int.MaxValue }, 10m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1));
        Assert.Equal("Viena ar daugiau sąskaitų nerasta.", ex.Message);
    }

    [Fact]
    public async Task RegisterPayment_FullyPaidInvoice_MessageNamesInvoiceNumber()
    {
        int partnerId = 0, invoiceId = 0;
        string number = $"INV-{Guid.NewGuid():N}";
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        try
        {
            var partner = new BusinessPartner
            {
                PartnerType = PartnerType.Customer, Name = $"Test Customer {Guid.NewGuid():N}", Country = "Lithuania",
                CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            ctx.BusinessPartners.Add(partner);
            await ctx.SaveChangesAsync();
            partnerId = partner.Id;
            var invoice = new Invoice
            {
                InvoiceNumber = number, InvoiceDate = DateTime.UtcNow, CustomerId = partnerId, Language = "lt",
                InvoiceType = "PVM SĄSKAITA FAKTŪRA", TotalInclVat = 50m, Status = InvoiceStatus.Confirmed,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            ctx.Invoices.Add(invoice);
            await ctx.SaveChangesAsync();
            invoiceId = invoice.Id;

            var service = new PaymentService(_fixture.Factory);
            await service.RegisterPaymentAsync(new List<int> { invoiceId }, 50m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterPaymentAsync(new List<int> { invoiceId }, 10m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1));
            Assert.Equal($"Sąskaita {number} jau pilnai apmokėta.", ex.Message);
        }
        finally
        {
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM payment_audit_log WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", partnerId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM payment_allocations WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", partnerId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE customer_id = {0}", partnerId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", invoiceId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partnerId);
        }
    }

    private sealed class NullAuth : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class DefaultSettings : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
