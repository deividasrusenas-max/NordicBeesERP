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
}
