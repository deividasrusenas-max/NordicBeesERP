using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for PaymentService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class PaymentServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public PaymentServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdatePaymentAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // Insert a real BusinessPartner (Invoice.CustomerId is FK to business_partners)
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            Name = $"Test Customer {Guid.NewGuid():N}",
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
        var partnerId = partner.Id;

        // Insert a minimal valid Invoice (Payment.InvoiceId is nullable but
        // the service reads it for recalculation — having a real invoice
        // avoids null issues in RecalculateInvoiceStatusInternalAsync).
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString("N")}",
            InvoiceDate = DateTime.UtcNow,
            CustomerId = partnerId,
            Language = "lt",
            InvoiceType = "PVM SĄSKAITA FAKTŪRA",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        var invoiceId = invoice.Id;

        // Insert a minimal valid Payment
        var payment = new Payment
        {
            PaymentDate = DateTime.UtcNow,
            CustomerId = partnerId,
            Amount = 10m,
            PaymentMethod = PaymentMethod.Cash,
            Source = "manual",
            InvoiceId = invoiceId,
            ReferenceNumber = $"REF-{Guid.NewGuid().ToString("N")}",
            Notes = $"NOTE-{Guid.NewGuid().ToString("N")}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        var paymentId = payment.Id;

        var service = new PaymentService(_fixture.Factory);

        await service.UpdatePaymentAsync(
            paymentId,
            123.45m,
            DateTime.UtcNow,
            "bank_transfer",
            $"REF-{Guid.NewGuid().ToString("N")}",
            $"NOTE-{Guid.NewGuid().ToString("N")}",
            1);

        // Verify with a BRAND NEW DbContext
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        Assert.NotNull(reloaded);
        Assert.Equal(123.45m, reloaded!.Amount);
        Assert.Equal(PaymentMethod.BankTransfer, reloaded.PaymentMethod);

        // Clean up
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM payments WHERE id = {0}", paymentId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    [Fact]
    public async Task UpdatePaymentAsync_SplitPayment_DateEditKeepsAllocations_AmountEditRejected()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer, Name = $"Test Customer {Guid.NewGuid():N}", Country = "Lithuania",
            CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();

        var invoiceIds = new List<int>();
        foreach (var _ in new[] { 0, 1 })
        {
            var inv = new Invoice
            {
                InvoiceNumber = $"INV-{Guid.NewGuid():N}", InvoiceDate = DateTime.UtcNow, CustomerId = partner.Id,
                Language = "lt", InvoiceType = "PVM SĄSKAITA FAKTŪRA", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            context.Invoices.Add(inv);
            await context.SaveChangesAsync();
            invoiceIds.Add(inv.Id);
        }

        var payment = new Payment
        {
            PaymentDate = DateTime.UtcNow, CustomerId = partner.Id, Amount = 1000m, PaymentMethod = PaymentMethod.BankTransfer,
            Source = "manual", InvoiceId = invoiceIds[0], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO payment_allocations (payment_id, invoice_id, allocated_amount, allocated_at) VALUES ({0}, {1}, {2}, NOW())",
            payment.Id, invoiceIds[0], 600m);
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO payment_allocations (payment_id, invoice_id, allocated_amount, allocated_at) VALUES ({0}, {1}, {2}, NOW())",
            payment.Id, invoiceIds[1], 400m);

        var service = new PaymentService(_fixture.Factory);

        // Amount change on a split payment is rejected
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePaymentAsync(payment.Id, 600m, DateTime.UtcNow, "bank_transfer", null, null, 1));

        // Date-only edit (total unchanged) keeps both allocations as they were
        await service.UpdatePaymentAsync(payment.Id, 1000m, DateTime.UtcNow.AddDays(-1), "bank_transfer", null, null, 1);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var allocs = await verify.PaymentAllocations.AsNoTracking()
            .Where(a => a.PaymentId == payment.Id).OrderBy(a => a.AllocatedAmount).Select(a => a.AllocatedAmount).ToListAsync();
        Assert.Equal(new[] { 400m, 600m }, allocs);
        Assert.Equal(1000m, (await verify.Payments.AsNoTracking().FirstAsync(p => p.Id == payment.Id)).Amount);

        await verify.Database.ExecuteSqlRawAsync("DELETE FROM payment_allocations WHERE payment_id = {0}", payment.Id);
        await verify.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE id = {0}", payment.Id);
        foreach (var id in invoiceIds)
            await verify.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", id);
        await verify.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partner.Id);
    }

    // ---------------------------------------------------------------
    // Payment write integrity (register validation, duplicate guard, credit-aware allocation,
    // audit kept on delete, update allocation cap). Each test seeds its own customer(s)/invoices
    // and removes everything in finally; assertions use a fresh context.
    // ---------------------------------------------------------------

    private sealed record Seed(List<int> PartnerIds, List<int> InvoiceIds);

    private async Task<int> SeedPartnerAsync(Seed seed)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer, Name = $"Test Customer {Guid.NewGuid():N}", Country = "Lithuania",
            CountryCode = "LT", DefaultLanguage = "LT", PaymentTermDays = 14, DefaultVatRate = 21m, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.BusinessPartners.Add(partner);
        await ctx.SaveChangesAsync();
        seed.PartnerIds.Add(partner.Id);
        return partner.Id;
    }

    private async Task<int> SeedInvoiceAsync(Seed seed, int partnerId, decimal total, InvoiceStatus status = InvoiceStatus.Confirmed)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-{Guid.NewGuid():N}", InvoiceDate = DateTime.UtcNow, CustomerId = partnerId,
            Language = "lt", InvoiceType = "PVM SĄSKAITA FAKTŪRA", TotalInclVat = total, Status = status,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();
        seed.InvoiceIds.Add(invoice.Id);
        return invoice.Id;
    }

    private async Task CleanupAsync(Seed seed)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        foreach (var partnerId in seed.PartnerIds)
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "DELETE FROM payment_audit_log WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", partnerId);
            await ctx.Database.ExecuteSqlRawAsync(
                "DELETE FROM payment_allocations WHERE payment_id IN (SELECT id FROM payments WHERE customer_id = {0})", partnerId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM payments WHERE customer_id = {0}", partnerId);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM credit_notes WHERE customer_id = {0}", partnerId);
        }
        foreach (var invoiceId in seed.InvoiceIds)
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", invoiceId);
        foreach (var partnerId in seed.PartnerIds)
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partnerId);
    }

    private async Task<int> CountPaymentsAsync(IEnumerable<int> partnerIds)
    {
        var ids = partnerIds.ToList();
        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        return await verify.Payments.AsNoTracking().CountAsync(p => ids.Contains(p.CustomerId));
    }

    private async Task<int> CountAllocationsAsync(IEnumerable<int> invoiceIds)
    {
        var ids = invoiceIds.ToList();
        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        return await verify.PaymentAllocations.AsNoTracking().CountAsync(a => ids.Contains(a.InvoiceId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RegisterPaymentAsync_NonPositiveAmount_ThrowsAndWritesNothing(int amount)
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m);
            var service = new PaymentService(_fixture.Factory);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterPaymentAsync(new List<int> { invoiceId }, amount, DateTime.UtcNow.Date, "bank_transfer", null, null, 1));

            Assert.Equal("Mokėjimo suma turi būti didesnė už 0.", ex.Message);
            Assert.Equal(0, await CountPaymentsAsync(seed.PartnerIds));
            Assert.Equal(0, await CountAllocationsAsync(seed.InvoiceIds));
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task RegisterPaymentAsync_InvoicesOfDifferentCustomers_ThrowsAndWritesNothing()
    {
        var seed = new Seed(new(), new());
        try
        {
            var inv1 = await SeedInvoiceAsync(seed, await SeedPartnerAsync(seed), 100m);
            var inv2 = await SeedInvoiceAsync(seed, await SeedPartnerAsync(seed), 100m);
            var service = new PaymentService(_fixture.Factory);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterPaymentAsync(new List<int> { inv1, inv2 }, 150m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1));

            Assert.Equal("Visos sąskaitos turi būti to paties kliento.", ex.Message);
            Assert.Equal(0, await CountPaymentsAsync(seed.PartnerIds));
            Assert.Equal(0, await CountAllocationsAsync(seed.InvoiceIds));
        }
        finally { await CleanupAsync(seed); }
    }

    [Theory]
    [InlineData(InvoiceStatus.Cancelled)]
    [InlineData(InvoiceStatus.Draft)]
    public async Task RegisterPaymentAsync_DraftOrCancelledInvoice_ThrowsAndWritesNothing(InvoiceStatus status)
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m, status);
            var service = new PaymentService(_fixture.Factory);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterPaymentAsync(new List<int> { invoiceId }, 50m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1));

            Assert.Equal("Negalima registruoti mokėjimo juodraščiui ar anuliuotai sąskaitai.", ex.Message);
            Assert.Equal(0, await CountPaymentsAsync(seed.PartnerIds));
            Assert.Equal(0, await CountAllocationsAsync(seed.InvoiceIds));
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task RegisterPaymentAsync_SamePaymentWithin120Seconds_RejectedThenAllowedAfterWindow()
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 1000m);
            var service = new PaymentService(_fixture.Factory);
            var date = DateTime.UtcNow.Date;
            var ids = new List<int> { invoiceId };

            await service.RegisterPaymentAsync(ids, 100m, date, "bank_transfer", null, null, 1);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RegisterPaymentAsync(ids, 100m, date, "bank_transfer", null, null, 1));
            Assert.Equal("Toks mokėjimas ką tik užregistruotas – patikrinkite mokėjimų sąrašą.", ex.Message);
            Assert.Equal(1, await CountPaymentsAsync(seed.PartnerIds));

            await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "UPDATE payments SET created_at = DATE_SUB(UTC_TIMESTAMP(), INTERVAL 121 SECOND) WHERE customer_id = {0}", partnerId);
            }

            await service.RegisterPaymentAsync(ids, 100m, date, "bank_transfer", null, null, 1);
            Assert.Equal(2, await CountPaymentsAsync(seed.PartnerIds));
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task RegisterPaymentAsync_InvoiceWithCreditNote_AllocatesOnlyRemainingAndMarksPaid()
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m);
            await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            {
                await ctx.Database.ExecuteSqlRawAsync(
                    "INSERT INTO credit_notes (credit_note_number, credit_date, original_invoice_id, applied_invoice_id, customer_id, total_incl_vat, status, created_at, updated_at) VALUES ({0}, {1}, {2}, {2}, {3}, {4}, {5}, {6}, {6})",
                    $"CN-{Guid.NewGuid():N}", DateTime.UtcNow.Date, invoiceId, partnerId, 30m, "printed", DateTime.UtcNow);
            }
            var service = new PaymentService(_fixture.Factory);

            var paymentId = await service.RegisterPaymentAsync(new List<int> { invoiceId }, 100m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1);

            await using var verify = await _fixture.Factory.CreateDbContextAsync();
            var allocation = await verify.PaymentAllocations.AsNoTracking().SingleAsync(a => a.PaymentId == paymentId);
            Assert.Equal(70m, allocation.AllocatedAmount);
            Assert.Equal(100m, (await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId)).Amount);
            var invoice = await verify.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
            Assert.Equal(InvoiceStatus.Paid, invoice.Status);
            Assert.Equal("paid", invoice.PaymentStatus);
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task DeletePaymentAsync_RemovesPaymentAndAllocations_KeepsAuditHistoryAndAddsDeleteRow()
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m);
            var service = new PaymentService(_fixture.Factory);

            var paymentId = await service.RegisterPaymentAsync(new List<int> { invoiceId }, 100m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1);
            await service.UpdatePaymentAsync(paymentId, 100m, DateTime.UtcNow.Date.AddDays(-1), "cash", null, null, 1);

            Assert.True(await service.DeletePaymentAsync(paymentId, 1));

            await using var verify = await _fixture.Factory.CreateDbContextAsync();
            Assert.False(await verify.Payments.AsNoTracking().AnyAsync(p => p.Id == paymentId));
            Assert.False(await verify.PaymentAllocations.AsNoTracking().AnyAsync(a => a.PaymentId == paymentId));
            var invoice = await verify.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
            Assert.Equal(0m, invoice.PaidAmount);
            Assert.Equal("unpaid", invoice.PaymentStatus);
            var actions = await verify.PaymentAuditLogs.AsNoTracking()
                .Where(a => a.PaymentId == paymentId).OrderBy(a => a.Id).Select(a => a.Action).ToListAsync();
            Assert.Equal(new[] { "create", "update", "delete" }, actions);
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task UpdatePaymentAsync_SingleAllocation_AmountAboveRemaining_CapsAllocation()
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m);
            var service = new PaymentService(_fixture.Factory);
            var paymentId = await service.RegisterPaymentAsync(new List<int> { invoiceId }, 60m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1);

            await service.UpdatePaymentAsync(paymentId, 150m, DateTime.UtcNow.Date, "bank_transfer", null, null, 1);

            await using var verify = await _fixture.Factory.CreateDbContextAsync();
            Assert.Equal(150m, (await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId)).Amount);
            Assert.Equal(100m, (await verify.PaymentAllocations.AsNoTracking().SingleAsync(a => a.PaymentId == paymentId)).AllocatedAmount);
            var invoice = await verify.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
            Assert.True(invoice.PaidAmount <= invoice.TotalInclVat);
            Assert.Equal(100m, invoice.PaidAmount);
        }
        finally { await CleanupAsync(seed); }
    }

    [Fact]
    public async Task UpdatePaymentAsync_ChangesMethodAndFields_PersistedByRawUpdate()
    {
        var seed = new Seed(new(), new());
        try
        {
            var partnerId = await SeedPartnerAsync(seed);
            var invoiceId = await SeedInvoiceAsync(seed, partnerId, 100m);
            var service = new PaymentService(_fixture.Factory);
            var paymentId = await service.RegisterPaymentAsync(new List<int> { invoiceId }, 40m, DateTime.UtcNow.Date, "bank_transfer", "OLD-REF", "old note", 1);

            var newDate = DateTime.UtcNow.Date.AddDays(-3);
            await service.UpdatePaymentAsync(paymentId, 55m, newDate, "cash", "NEW-REF", null, 1);

            await using var verify = await _fixture.Factory.CreateDbContextAsync();
            var method = await RawSql.ScalarAsync(verify, "SELECT payment_method FROM payments WHERE id = @p0", default, paymentId);
            Assert.Equal("cash", method);
            var payment = await verify.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId);
            Assert.Equal(PaymentMethod.Cash, payment.PaymentMethod);
            Assert.Equal(55m, payment.Amount);
            Assert.Equal(newDate, payment.PaymentDate.Date);
            Assert.Equal("NEW-REF", payment.ReferenceNumber);
            Assert.Null(payment.Notes);
            Assert.Equal(55m, (await verify.PaymentAllocations.AsNoTracking().SingleAsync(a => a.PaymentId == paymentId)).AllocatedAmount);
        }
        finally { await CleanupAsync(seed); }
    }

    // Transaction rollback test (#8) intentionally not written: every failure point after the payment insert in
    // RegisterPaymentAsync needs either a DB-level fault (DDL/trigger - forbidden for agents) or a test seam in
    // production code, and the task says to skip it in that case.
}
