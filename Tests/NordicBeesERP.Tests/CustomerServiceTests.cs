using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for CustomerService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows, or silently coerces NULL into an empty string.
/// </summary>
public class CustomerServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public CustomerServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateBusinessPartnerAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var uniqueName = $"Test Customer Partner {Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        // Insert via raw SQL (no SaveChangesAsync — tracked context insert is fine for inserts,
        // but caller requires ExecuteSqlRawAsync for setup/teardown)
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO business_partners " +
            "(partner_type, name, company_code, is_active, country, country_code, default_vat_rate, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
            "customer", uniqueName, "TPC", 1, "Lithuania", "LT", 0m, now, now);

        // Read back to get the auto-incremented id
        var id = await context.BusinessPartners
            .AsNoTracking()
            .Where(bp => bp.Name == uniqueName)
            .Select(bp => bp.Id)
            .FirstOrDefaultAsync();

        var service = new CustomerService(_fixture.Factory, NullLogger<CustomerService>.Instance);

        // Create a BusinessPartner object with updated fields
        var partner = new BusinessPartner
        {
            Id = id,
            PartnerType = PartnerType.Customer,
            Name = "Updated Customer",
            City = "Vilnius",
            VatCode = "LT1234567890",
            CompanyCode = "TPC",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 7,
            DefaultVatRate = 21m,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await service.UpdateBusinessPartnerAsync(partner);

        // Verify with a fresh context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated Customer", reloaded!.Name);
        Assert.Equal("Vilnius", reloaded.City);
        Assert.Equal("LT1234567890", reloaded.VatCode);

        // Clean up
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }
}
