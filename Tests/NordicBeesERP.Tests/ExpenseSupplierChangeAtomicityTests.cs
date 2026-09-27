using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 2 fix-up A2 (D-010): a supplier assignment/change and its alias confirmation are one transaction. Before this
/// fix, <c>SupplierAliases.ConfirmAsync</c> ran AFTER <c>transaction.CommitAsync()</c> in both <c>AssignSupplierAsync</c> and
/// <c>ChangeSupplierAsync</c> — a failure there left the supplier already committed behind the exception the user saw. These
/// tests inject a real ADO.NET-level failure into the alias step (an EF Core <see cref="DbCommandInterceptor"/> that throws
/// on the <c>supplier_alias_events</c> INSERT, the one write every confirmation makes) and verify nothing was saved: the
/// invoice's supplier, status and flags are unchanged, no audit row and no alias row exist. Real nordic_bees_erp_test.
/// </summary>
public class ExpenseSupplierChangeAtomicityTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ExpenseSupplierChangeAtomicityTests(DbTestFixture fixture) { _fixture = fixture; }

    private ExpenseService CreateService(IDbContextFactory<NordicBeesERPContext> factory) => new(factory, new NullAuth(), new DefaultSettings());

    /// <summary>Throws before a write whose SQL text contains <paramref name="needle"/> reaches the database — everything else passes through untouched.</summary>
    private sealed class ThrowOnWriteInterceptor : DbCommandInterceptor
    {
        private readonly string _needle;
        public ThrowOnWriteInterceptor(string needle) { _needle = needle; }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) =>
            command.CommandText.Contains(_needle, StringComparison.Ordinal) ? throw Fault() : result;

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            command.CommandText.Contains(_needle, StringComparison.Ordinal) ? throw Fault() : new ValueTask<InterceptionResult<int>>(result);

        private static InvalidOperationException Fault() => new("Injected test failure: the alias step");
    }

    /// <summary>A second, independent DbContextFactory on the same test database, with the alias-event write wired to fail.</summary>
    private static IDbContextFactory<NordicBeesERPContext> FaultyFactory()
    {
        var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
            ?? "Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;";
        var services = new ServiceCollection();
        services.AddDbContextFactory<NordicBeesERPContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .AddInterceptors(new ThrowOnWriteInterceptor("INSERT INTO supplier_alias_events")));
        return services.BuildServiceProvider().GetRequiredService<IDbContextFactory<NordicBeesERPContext>>();
    }

    private readonly List<int> _partners = new();
    private readonly List<int> _invoices = new();

    private async Task<int> InsertPartnerAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier, Name = $"A2 Partner {Guid.NewGuid():N}", Country = "Lithuania", CountryCode = "LT",
            DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsSupplier = true, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        _partners.Add(partner.Id);
        return partner.Id;
    }

    private async Task<int> InsertInvoiceAsync(string status, int? supplierId, string pendingName)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var marker = $"A2-{Guid.NewGuid():N}";
        var date = DateTime.Today;
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, status, supplier_id, ocr_flags, notes, currency, source, ocr_status, pending_supplier_name, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, 100, 21, 21, 121, {3}, {4}, '[]', {5}, 'EUR', 'MANUAL', 'COMPLETED', {6}, NOW(), NOW())",
            marker, date, date.AddDays(30), status, supplierId, marker, pendingName);
        var id = await context.ExpenseInvoices.Where(i => i.Notes == marker).Select(i => i.Id).FirstAsync();
        _invoices.Add(id);
        return id;
    }

    private async Task<ExpenseInvoice> ReloadAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == id);
    }

    private async Task<int> AuditCountAsync(int invoiceId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.ExpenseInvoiceAudits.AsNoTracking().CountAsync(a => a.InvoiceId == invoiceId);
    }

    private async Task<bool> AliasExistsAsync(string rawName, int partnerId)
    {
        var key = SupplierAliases.KeyFor(rawName)!;
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        return await context.SupplierAliases.AsNoTracking().AnyAsync(a => a.AliasKey == key && a.PartnerId == partnerId);
    }

    private async Task CleanupAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var partner in _partners)
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM supplier_alias_events WHERE alias_id IN (SELECT id FROM supplier_aliases WHERE partner_id = {0})", partner);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM supplier_aliases WHERE partner_id = {0}", partner);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partner);
        }
        foreach (var invoice in _invoices)
        {
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoice);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE id = {0}", invoice);
        }
    }

    [Fact]
    public async Task AssignSupplierAsync_AliasStepFails_RollsBackTheWholeAssignment()
    {
        var partner = await InsertPartnerAsync();
        var name = $"A2 Raw {Guid.NewGuid():N}";
        var invoice = await InsertInvoiceAsync("PENDING_SUPPLIER", null, name);
        try
        {
            var faulty = CreateService(FaultyFactory());
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => faulty.AssignSupplierAsync(invoice, partner, "Test User"));
            Assert.Contains("alias", ex.Message, StringComparison.OrdinalIgnoreCase);

            var stored = await ReloadAsync(invoice);
            Assert.Null(stored.SupplierId);
            Assert.Equal("PENDING_SUPPLIER", stored.Status);
            Assert.Equal(0, await AuditCountAsync(invoice));   // the SUPPLIER_ASSIGNED audit row was rolled back too
            Assert.False(await AliasExistsAsync(name, partner));

            // and the healthy path still works normally on the same rows — the injected fault did not corrupt anything
            await CreateService(_fixture.Factory).AssignSupplierAsync(invoice, partner, "Test User");
            Assert.Equal(partner, (await ReloadAsync(invoice)).SupplierId);
        }
        finally { await CleanupAsync(); }
    }

    [Fact]
    public async Task ChangeSupplierAsync_AliasStepFails_RollsBackTheWholeChange()
    {
        var oldPartner = await InsertPartnerAsync();
        var newPartner = await InsertPartnerAsync();
        var name = $"A2 Raw {Guid.NewGuid():N}";
        var invoice = await InsertInvoiceAsync("PENDING", oldPartner, name);
        try
        {
            var faulty = CreateService(FaultyFactory());
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => faulty.ChangeSupplierAsync(invoice, newPartner, "Test User"));
            Assert.Contains("alias", ex.Message, StringComparison.OrdinalIgnoreCase);

            var stored = await ReloadAsync(invoice);
            Assert.Equal(oldPartner, stored.SupplierId);   // unchanged — the UPDATE was rolled back too
            Assert.Equal("PENDING", stored.Status);
            Assert.Equal(0, await AuditCountAsync(invoice)); // no SUPPLIER_CHANGED row either
            Assert.False(await AliasExistsAsync(name, newPartner));

            await CreateService(_fixture.Factory).ChangeSupplierAsync(invoice, newPartner, "Test User");
            Assert.Equal(newPartner, (await ReloadAsync(invoice)).SupplierId);
        }
        finally { await CleanupAsync(); }
    }

    // The dialog's existing generic catch (Components/Dialogs/InvoiceDetailDialog.razor, AssignPartnerAsync / ChangeSupplierAsync:
    // catch (Exception ex) => Snackbar.Add("Klaida ... : " + ex.Message, Severity.Error)) already turns whatever this throws into a
    // Lithuanian-prefixed message — no UI change needed for A2; this only guards that the service keeps throwing (never swallows).

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
