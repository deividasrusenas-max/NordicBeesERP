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
}
