using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// FROZEN.md behavior tests for SupplierService. These are integration
/// tests against the real nordic_bees_erp_test database (global
/// QueryTrackingBehavior.NoTracking, same as production) — they exist to
/// catch the exact bug class this codebase has hit repeatedly: a write
/// method that appears to succeed (no exception) but silently persists
/// zero rows, or silently coerces NULL into an empty string.
/// </summary>
public class SupplierServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public SupplierServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private static BusinessPartner NewTestPartner(string name) => new()
    {
        PartnerType = PartnerType.Supplier,
        Name = name,
        Country = "Lithuania",
        CountryCode = "LT",
        DefaultLanguage = "LT",
        PaymentTermDays = 14,
        DefaultVatRate = 21m,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        Email = "original@example.com"
    };

    [Fact]
    public async Task UpdateBusinessPartnerAsync_PersistsChangesToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test Supplier {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        partner.Name = "Updated Name";
        await service.UpdateBusinessPartnerAsync(partner);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated Name", reloaded!.Name);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task UpdateBusinessPartnerAsync_NullEmail_PersistsAsSqlNullNotEmptyString()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test Supplier {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        partner.Email = null;
        await service.UpdateBusinessPartnerAsync(partner);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var isNullCount = await verifyContext.BusinessPartners
            .FromSqlRaw("SELECT * FROM business_partners WHERE id = {0} AND email IS NULL", id)
            .AsNoTracking()
            .CountAsync();

        Assert.Equal(1, isNullCount);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task UpdateBusinessPartnerAsync_PersistsCompensationVatCodeToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test Supplier {Guid.NewGuid():N}");
        partner.CompensationVatCode = "100008534429";
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        await service.UpdateBusinessPartnerAsync(partner);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("100008534429", reloaded!.CompensationVatCode);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task SaveSupplierAsync_Insert_PersistsRoleFlagsAndDerivesBothPartnerType()
    {
        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = 0,
            Name = $"Test Both Partner {Guid.NewGuid():N}",
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
            IsIndividual = false
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);

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

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_Update_PersistsRoleFlagsAndDerivesSupplierPartnerType()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test Supplier Update {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = id,
            Name = partner.Name,
            City = "Kaunas",
            CountryCode = partner.CountryCode,
            Country = partner.Country,
            DefaultLanguage = partner.DefaultLanguage,
            PaymentTermDays = partner.PaymentTermDays,
            DefaultVatRate = partner.DefaultVatRate,
            IsActive = partner.IsActive,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = false
        };

        await service.SaveSupplierAsync(dto);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.False(reloaded!.IsCustomer);
        Assert.True(reloaded.IsSupplier);
        Assert.False(reloaded.IsExpenseSupplier);
        Assert.False(reloaded.IsIndividual);
        Assert.Equal("supplier", reloaded.PartnerType.ToString().ToLower());

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task SaveSupplierAsync_Insert_PersistsVatVerificationFields()
    {
        var service = new SupplierService(_fixture.Factory);

        var verifiedAt = DateTime.UtcNow;
        var dto = new Supplier
        {
            Id = 0,
            Name = $"Test VIES Insert {Guid.NewGuid():N}",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = false,
            VatVerified = true,
            VatVerifiedAt = verifiedAt,
            VatVerifiedName = "Test VIES Name LT"
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.VatVerified);
        Assert.Equal(verifiedAt, reloaded.VatVerifiedAt);
        Assert.Equal("Test VIES Name LT", reloaded.VatVerifiedName);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_Update_PersistsVatVerificationFields()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test VIES Update {Guid.NewGuid():N}");
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        var verifiedAt = DateTime.UtcNow;
        var dto = new Supplier
        {
            Id = id,
            Name = partner.Name,
            City = "Kaunas",
            CountryCode = partner.CountryCode,
            Country = partner.Country,
            DefaultLanguage = partner.DefaultLanguage,
            PaymentTermDays = partner.PaymentTermDays,
            DefaultVatRate = partner.DefaultVatRate,
            IsActive = partner.IsActive,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = false,
            VatVerified = true,
            VatVerifiedAt = verifiedAt,
            VatVerifiedName = "Test VIES Name LT"
        };

        await service.SaveSupplierAsync(dto);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.True(reloaded!.VatVerified);
        Assert.Equal(verifiedAt, reloaded.VatVerifiedAt);
        Assert.Equal("Test VIES Name LT", reloaded.VatVerifiedName);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task SaveSupplierAsync_UpdateBranch_PersistsFarmerFieldsToRealDatabase()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var partner = NewTestPartner($"Test Farmer {Guid.NewGuid():N}");
        partner.DefaultVatRate = 6m;
        partner.IsIndividual = true;
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        var id = partner.Id;

        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = id,
            Name = partner.Name,
            City = "Kaunas",
            CountryCode = partner.CountryCode,
            Country = partner.Country,
            DefaultLanguage = partner.DefaultLanguage,
            PaymentTermDays = partner.PaymentTermDays,
            DefaultVatRate = 6m,
            IsActive = partner.IsActive,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = "TestF",
            SupplierLastName = "Farmer",
            NationalIdNumber = "10000000098",
            SupplierType = "Farmer",
            CompensationVatCode = "100008534429"
        };

        await service.SaveSupplierAsync(dto);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("TestF", reloaded!.SupplierFirstName);
        Assert.Equal("Farmer", reloaded.SupplierLastName);
        Assert.Equal("10000000098", reloaded.NationalIdNumber);
        Assert.Equal("Farmer", reloaded.SupplierType);
        Assert.Equal("100008534429", reloaded.CompensationVatCode);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task SaveSupplierAsync_InsertBranch_PersistsCompensationVatCodeToRealDatabase()
    {
        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = 0,
            Name = $"Test Farmer Insert {Guid.NewGuid():N}",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 6m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = "TestF",
            SupplierLastName = "Farmer",
            SupplierType = "Farmer",
            CompensationVatCode = "100008534429"
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("100008534429", reloaded!.CompensationVatCode);
        Assert.Equal("TestF", reloaded.SupplierFirstName);
        Assert.Equal("Farmer", reloaded.SupplierLastName);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_IncompleteFarmer_SavesWithWarnings()
    {
        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = 0,
            Name = $"Test Farmer Warn {Guid.NewGuid():N}",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 6m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = "TestF",
            SupplierLastName = null,
            CompensationVatCode = null
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);
        Assert.NotEmpty(saved.SaveWarnings);
        Assert.Contains(saved.SaveWarnings, w => w.Contains("pavardės"));

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_InsertIndividual_AutoDerivesNameFromFirstAndLastName()
    {
        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = 0,
            Name = "OLD NAME",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 6m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = "TestNameF",
            SupplierLastName = "NameL",
            SupplierType = "Farmer"
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("TestNameF NameL", reloaded!.Name);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_InsertIndividual_LegacyNameNotWipedWhenFirstAndLastNameEmpty()
    {
        var service = new SupplierService(_fixture.Factory);

        var legacyName = $"Legacy Farmer Name {Guid.NewGuid():N}";
        var dto = new Supplier
        {
            Id = 0,
            Name = legacyName,
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 6m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = null,
            SupplierLastName = null,
            SupplierType = "Farmer"
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(legacyName, reloaded!.Name);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }

    [Fact]
    public async Task SaveSupplierAsync_CompleteFarmer_NoWarnings()
    {
        var service = new SupplierService(_fixture.Factory);

        var dto = new Supplier
        {
            Id = 0,
            Name = $"Test Farmer OK {Guid.NewGuid():N}",
            City = "Vilnius",
            CountryCode = "LT",
            Country = "Lithuania",
            DefaultLanguage = "lt",
            PaymentTermDays = 14,
            DefaultVatRate = 6m,
            IsActive = true,
            IsCustomer = false,
            IsSupplier = true,
            IsExpenseSupplier = false,
            IsIndividual = true,
            SupplierFirstName = "TestF",
            SupplierLastName = "Farmer",
            SupplierType = "Farmer",
            NationalIdNumber = "10000000098",
            Address = "Testo g. 5, Vilnius",
            BankAccount = "LT121000011101001000",
            CompensationVatCode = "100008534429"
        };

        var saved = await service.SaveSupplierAsync(dto);

        Assert.True(saved.Id > 0);
        Assert.Empty(saved.SaveWarnings);

        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.BusinessPartners
            .AsNoTracking()
            .FirstOrDefaultAsync(bp => bp.Id == saved.Id);

        Assert.NotNull(reloaded);

        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM business_partners WHERE id = {0}", saved.Id);
    }
}
