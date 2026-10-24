using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dunning;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// nordic_bees_erp_test only gets the settlement tables when a human applies Migrations/Scripts/20261024_settlement_offsets.sql
/// (DDL is never run by an agent). Until then these tests skip instead of failing.
/// </summary>
public sealed class FactRequiresSettlementTablesAttribute : FactAttribute
{
    public FactRequiresSettlementTablesAttribute()
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
                ?? "Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;";
            using var connection = new MySqlConnector.MySqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name IN ('settlement_offsets','settlement_offset_lines')";
            if (Convert.ToInt32(command.ExecuteScalar()) < 2)
                Skip = "Test DB lacks settlement_offsets (apply Migrations/Scripts/20261024_settlement_offsets.sql to nordic_bees_erp_test)";
        }
        catch (Exception ex)
        {
            Skip = "Test DB not reachable: " + ex.GetType().Name;
        }
    }
}

[Collection("RealDatabase")]
public class SettlementOffsetServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public SettlementOffsetServiceTests(DbTestFixture fixture) => _fixture = fixture;

    private sealed class World
    {
        public int PartnerId, OtherPartnerId, LakId, ExpenseId;
        public List<int> ExtraInvoiceIds = new();
        public List<int> ExtraExpenseIds = new();
    }

    private SettlementOffsetService Service() => new(_fixture.Factory);

    private async Task<int> NewPartnerAsync(NordicBeesERPContextHolder h)
    {
        var p = new BusinessPartner
        {
            PartnerType = PartnerType.Customer, IsCustomer = true, IsSupplier = true, IsExpenseSupplier = true, Name = $"Offset Partner {Guid.NewGuid():N}",
            Country = "Lithuania", CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        h.Db.BusinessPartners.Add(p);
        await h.Db.SaveChangesAsync();
        return p.Id;
    }

    private sealed class NordicBeesERPContextHolder : IAsyncDisposable
    {
        public NordicBeesERP.Data.NordicBeesERPContext Db { get; }
        public NordicBeesERPContextHolder(NordicBeesERP.Data.NordicBeesERPContext db) => Db = db;
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private static Invoice NewLak(int partnerId, decimal total, InvoiceStatus status = InvoiceStatus.Confirmed, DateTime? date = null) => new()
    {
        InvoiceNumber = $"LAK-OFF-{Guid.NewGuid():N}".Substring(0, 24), InvoiceDate = date ?? new DateTime(2026, 3, 1), CustomerId = partnerId,
        Language = "lt", InvoiceType = "PVM SĄSKAITA FAKTŪRA", TotalInclVat = total, SubtotalExclVat = total, Status = status,
        PaymentStatus = "unpaid", PaymentDueDate = new DateTime(2026, 3, 15), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static ExpenseInvoice NewExpense(int supplierId, decimal total, DateTime? date = null, string currency = "EUR") => new()
    {
        SupplierId = supplierId, InvoiceNumber = $"EXP-OFF-{Guid.NewGuid():N}".Substring(0, 24), InvoiceDate = date ?? new DateTime(2026, 3, 1),
        DueDate = new DateTime(2026, 3, 31), AmountExclVat = total, VatAmount = 0m, AmountInclVat = total, Status = "APPROVED", OcrStatus = "PENDING",
        Currency = currency, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private async Task<World> SeedAsync(decimal lak = 120m, decimal expense = 20m)
    {
        await using var h = new NordicBeesERPContextHolder(await _fixture.Factory.CreateDbContextAsync());
        var w = new World { PartnerId = await NewPartnerAsync(h), OtherPartnerId = await NewPartnerAsync(h) };
        var inv = NewLak(w.PartnerId, lak);
        var exp = NewExpense(w.PartnerId, expense);
        h.Db.Invoices.Add(inv);
        h.Db.ExpenseInvoices.Add(exp);
        await h.Db.SaveChangesAsync();
        w.LakId = inv.Id;
        w.ExpenseId = exp.Id;
        return w;
    }

    private async Task CleanupAsync(World w)
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var partners = new[] { w.PartnerId, w.OtherPartnerId };
        foreach (var pid in partners)
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM settlement_offset_lines WHERE offset_id IN (SELECT id FROM settlement_offsets WHERE partner_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM settlement_offsets WHERE partner_id = {0}", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM payment_audit_log WHERE invoice_id IN (SELECT id FROM invoices WHERE customer_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM payment_audit_log WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM payment_allocations WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE customer_id = {0}", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_payments WHERE invoice_id IN (SELECT id FROM expense_invoices WHERE supplier_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoice_audit WHERE invoice_id IN (SELECT id FROM expense_invoices WHERE supplier_id = {0})", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM expense_invoices WHERE supplier_id = {0}", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE customer_id = {0}", pid);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", pid);
        }
    }

    private static CreateOffsetRequest Request(World w, decimal lakAmount, decimal expAmount, DateTime? date = null) => new()
    {
        PartnerId = w.PartnerId, OffsetDate = date ?? new DateTime(2026, 4, 1),
        Receivables = new() { new OffsetLineRequest { DocumentId = w.LakId, Amount = lakAmount } },
        Payables = new() { new OffsetLineRequest { DocumentId = w.ExpenseId, Amount = expAmount } }
    };

    private async Task<(decimal LakPaid, string LakStatus, decimal ExpPaid, string ExpStatus)> StateAsync(World w)
    {
        await using var db = await _fixture.Factory.CreateDbContextAsync();
        var lak = await db.Invoices.AsNoTracking().FirstAsync(i => i.Id == w.LakId);
        var exp = await db.ExpenseInvoices.AsNoTracking().FirstAsync(i => i.Id == w.ExpenseId);
        return (lak.PaidAmount, lak.PaymentStatus ?? "", exp.PaidAmount, exp.Status);
    }

    // ---- user's example ----

    [FactRequiresSettlementTables]
    public async Task Example_Lak120_Expense20_Act20_ThenPayment100_LakPaid_ExpensePaid()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            var service = Service();
            var id = await service.CreateAndConfirmAsync(Request(w, 20m, 20m), userId: 1);
            Assert.True(id > 0);

            var afterAct = await StateAsync(w);
            Assert.Equal(20m, afterAct.LakPaid);
            Assert.Equal("partial", afterAct.LakStatus);
            Assert.Equal(20m, afterAct.ExpPaid);
            Assert.Equal("PAID", afterAct.ExpStatus);
            Assert.Equal(100m, (await service.GetOpenReceivablesAsync(w.PartnerId)).Single().Open);

            await new PaymentService(_fixture.Factory).RegisterPaymentAsync(new List<int> { w.LakId }, 100m, new DateTime(2026, 4, 2), "bank_transfer", null, null, 1);
            var final = await StateAsync(w);
            Assert.Equal("paid", final.LakStatus);

            var detail = await service.GetAsync(id);
            Assert.Matches(@"^UZS-26-\d{4}$", detail!.Offset.OffsetNumber);
            Assert.Equal(2, detail.Lines.Count);
        }
        finally { await CleanupAsync(w); }
    }

    // ---- validation: every rule ----

    [FactRequiresSettlementTables]
    public async Task Validation_RejectsEachBrokenRule_AndWritesNothing()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            var service = Service();
            async Task AssertRejects(CreateOffsetRequest r, string contains)
            {
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAndConfirmAsync(r, 1));
                Assert.Contains(contains, ex.Message);
            }

            await AssertRejects(Request(w, 20m, 10m), "lygi");                                  // receivable sum != payable sum
            await AssertRejects(Request(w, 0m, 0m), "didesnė už 0");                            // amount must be > 0
            await AssertRejects(Request(w, 30m, 30m), "viršija");                               // expense open is only 20
            await AssertRejects(Request(w, 20m, 20m, new DateTime(2026, 2, 1)), "ankstesnė");   // offset date before documents
            var other = Request(w, 20m, 20m); other.PartnerId = w.OtherPartnerId;
            await AssertRejects(other, "nepriklauso");                                          // different partner

            await using var db = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(0, await db.SettlementOffsets.AsNoTracking().CountAsync(o => o.PartnerId == w.PartnerId));
            Assert.Equal((0m, "unpaid", 0m, "APPROVED"), await StateAsync(w));
        }
        finally { await CleanupAsync(w); }
    }

    [FactRequiresSettlementTables]
    public async Task Validation_DraftCancelledDisputedLak_PreEpoch_AndNonEur_AreNotOffered()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            await using (var db = await _fixture.Factory.CreateDbContextAsync())
            {
                db.Invoices.AddRange(NewLak(w.PartnerId, 50m, InvoiceStatus.Draft), NewLak(w.PartnerId, 50m, InvoiceStatus.Cancelled),
                    NewLak(w.PartnerId, 50m, InvoiceStatus.Disputed), NewLak(w.PartnerId, 50m, date: new DateTime(2025, 12, 31)));
                db.ExpenseInvoices.AddRange(NewExpense(w.PartnerId, 50m, date: new DateTime(2025, 12, 31)), NewExpense(w.PartnerId, 50m, currency: "USD"));
                await db.SaveChangesAsync();
            }
            var service = Service();
            Assert.Equal(new[] { w.LakId }, (await service.GetOpenReceivablesAsync(w.PartnerId)).Select(d => d.Id));
            Assert.Equal(new[] { w.ExpenseId }, (await service.GetOpenPayablesAsync(w.PartnerId)).Select(d => d.Id));
        }
        finally { await CleanupAsync(w); }
    }

    // ---- cancel ----

    [FactRequiresSettlementTables]
    public async Task Cancel_RestoresBothSides_StoresReason_AndCannotBeRepeated()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            var service = Service();
            var id = await service.CreateAndConfirmAsync(Request(w, 20m, 20m), 1);
            await service.CancelAsync(id, "Klaidingai pasirinkta sąskaita", userId: 2);

            // Same status recalculation as ExpenseService.DeletePaymentAsync: a PAID invoice whose payment goes away is PENDING again
            // (the APPROVED mark is not restored there either).
            Assert.Equal((0m, "unpaid", 0m, "PENDING"), await StateAsync(w));
            await using var db = await _fixture.Factory.CreateDbContextAsync();
            var offset = await db.SettlementOffsets.AsNoTracking().FirstAsync(o => o.Id == id);
            Assert.Equal("cancelled", offset.Status);
            Assert.Equal("Klaidingai pasirinkta sąskaita", offset.CancelReason);
            Assert.Equal(2, offset.CancelledBy);
            Assert.Equal(0, await db.Payments.AsNoTracking().CountAsync(p => p.CustomerId == w.PartnerId));
            Assert.Equal(0, await db.ExpensePayments.AsNoTracking().CountAsync(p => p.InvoiceId == w.ExpenseId));
            Assert.Equal(2, await db.SettlementOffsetLines.AsNoTracking().CountAsync(l => l.OffsetId == id)); // act is never hard-deleted

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(id, "x", 2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CancelAsync(id, " ", 2));
        }
        finally { await CleanupAsync(w); }
    }

    [FactRequiresSettlementTables]
    public async Task GeneratedPayments_CannotBeEditedOrDeletedFromPaymentDialogs()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            var id = await Service().CreateAndConfirmAsync(Request(w, 20m, 20m), 1);
            var number = (await Service().GetAsync(id))!.Offset.OffsetNumber;
            var expected = $"Užskaitos mokėjimą galima atšaukti tik atšaukus užskaitos aktą {number}.";

            await using var db = await _fixture.Factory.CreateDbContextAsync();
            var paymentId = (await db.Payments.AsNoTracking().FirstAsync(p => p.CustomerId == w.PartnerId)).Id;
            var expPayment = await db.ExpensePayments.AsNoTracking().FirstAsync(p => p.InvoiceId == w.ExpenseId);
            var payments = new PaymentService(_fixture.Factory);
            var expenses = new ExpenseService(_fixture.Factory, new NullAuth(), new DefaultSettings());

            Assert.Equal(expected, (await Assert.ThrowsAsync<InvalidOperationException>(() => payments.DeletePaymentAsync(paymentId, 1))).Message);
            Assert.Equal(expected, (await Assert.ThrowsAsync<InvalidOperationException>(() => payments.UpdatePaymentAsync(paymentId, 5m, DateTime.UtcNow, "cash", null, null, 1))).Message);
            Assert.Equal(expected, (await Assert.ThrowsAsync<InvalidOperationException>(() => expenses.DeletePaymentAsync(expPayment.Id))).Message);
            expPayment.Amount = 5m;
            Assert.Equal(expected, (await Assert.ThrowsAsync<InvalidOperationException>(() => expenses.UpdatePaymentAsync(expPayment))).Message);
        }
        finally { await CleanupAsync(w); }
    }

    // ---- exclusions ----

    [FactRequiresSettlementTables]
    public async Task DunningFreshness_IgnoresOffsetPayments()
    {
        var w = await SeedAsync(120m, 20m);
        try
        {
            var id = await Service().CreateAndConfirmAsync(Request(w, 20m, 20m, new DateTime(2099, 1, 1)), 1);
            Assert.True(id > 0);
            var (_, payment) = await new DunningRepository(_fixture.Factory).LoadFreshnessAsync();
            Assert.True(payment == null || payment.Value < new DateTime(2099, 1, 1), "offset payment date must not count as fresh payment data");
        }
        finally { await CleanupAsync(w); }
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
