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

    [Fact]
    public async Task SaveCustomerAsync_Insert_PersistsRoleFlagsAndDerivedPartnerType()
    {
        var service = new CustomerService(_fixture.Factory, NullLogger<CustomerService>.Instance);

        // The Both combination: IsCustomer + IsSupplier → derives legacy partner_type "both"
        var dto = new Customer
        {
            Id = 0,
            Name = $"Test Save Customer {Guid.NewGuid():N}",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            IsCustomer = true,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = false,
        };

        var saved = await service.SaveCustomerAsync(dto);

        // SaveCustomerAsync returns the DTO with the new row Id populated (INSERT branch)
        Assert.True(saved.Id > 0);

        // Verify with a brand-new context — proves the write reached the database
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsCustomer);
        Assert.True(reloaded.IsSupplier);
        Assert.False(reloaded.IsExpenseSupplier);
        Assert.False(reloaded.IsIndividual);
        Assert.Equal("both", reloaded.PartnerType.ToString().ToLower());

        // Clean up
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveCustomerAsync_Update_PersistsRoleFlagsAndDerivedPartnerType()
    {
        // Arrange: insert a base partner to get a real auto-incremented id
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var uniqueName = $"Test Update Customer {Guid.NewGuid():N}";
        var now = DateTime.UtcNow;

        var inserted = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = uniqueName,
            CompanyCode = "TPC",
            City = "Kaunas",
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "lt",
            PaymentTermDays = 7,
            DefaultVatRate = 0m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.BusinessPartners.Add(inserted);
        await context.SaveChangesAsync();
        var id = inserted.Id;
        Assert.True(id > 0);

        var service = new CustomerService(_fixture.Factory, NullLogger<CustomerService>.Instance);

        // Customer-only combination → derives legacy partner_type "customer"
        var dto = new Customer
        {
            Id = id,
            Name = uniqueName,
            CompanyCode = "TPC",
            City = "Kaunas",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 7,
            DefaultVatRate = 21m,
            IsActive = true,
            IsCustomer = true,
            IsSupplier = false,
            IsExpenseSupplier = false,
            IsIndividual = false,
        };

        await service.SaveCustomerAsync(dto);

        // Verify with a brand-new context — proves the UPDATE reached the database
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsCustomer);
        Assert.False(reloaded.IsSupplier);
        Assert.False(reloaded.IsExpenseSupplier);
        Assert.False(reloaded.IsIndividual);
        Assert.Equal("customer", reloaded.PartnerType.ToString().ToLower());

        // Clean up
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }
}
