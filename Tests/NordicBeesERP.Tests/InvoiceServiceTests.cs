using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for InvoiceService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows.
/// </summary>
public class InvoiceServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public InvoiceServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static BusinessPartner NewTestCustomer(string name) => new()
    {
        PartnerType = PartnerType.Customer,
        Name = name,
        Country = "Lithuania",
        CountryCode = "LT",
        DefaultLanguage = "LT",
        PaymentTermDays = 14,
        DefaultVatRate = 21m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    private static Invoice NewTestInvoice(int customerId, string invoiceNumber) => new()
    {
        InvoiceNumber = invoiceNumber,
        InvoiceDate = DateTime.UtcNow.Date,
        CustomerId = customerId,
        Language = "LT",
        InvoiceType = "PVM SĄSKAITA FAKTŪRA",
        Status = InvoiceStatus.Draft,
        TotalInclVat = 100m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task DeleteInvoiceAsync_RemovesRowFromRealDatabase()
    {
        // Arrange: insert a real BusinessPartner (FK target) and a minimal Invoice
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestCustomer($"Test Customer {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var partnerId = partner.Id;

        var invoice = NewTestInvoice(partnerId, $"INV-{Guid.NewGuid():N}");
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        var invoiceId = invoice.Id;

        var service = new InvoiceService(_fixture.Factory, null!, null!);

        // Act
        var result = await service.DeleteInvoiceAsync(invoiceId);

        // Assert: method reports deletion
        Assert.Equal(1, result);

        // Assert: row is actually gone from the database
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var remaining = await verifyContext.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        Assert.Null(remaining);

        // Cleanup (defensive)
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM invoices WHERE id = {0}", invoiceId);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", partnerId);
    }
}
