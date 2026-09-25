using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for ExpenseOcrService.FindSupplierIdAsync against the real
/// nordic_bees_erp_test database. Guards the production incident where an empty OCR
/// VAT code matched the first partner with an empty VAT code (PROD-DATA-FINDINGS §3),
/// and the D-017 rule that ambiguous matches must stay loud (null, null).
/// </summary>
public class ExpenseOcrServiceFindSupplierTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseOcrServiceFindSupplierTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Vies / company settings are not used by FindSupplierIdAsync.
    private ExpenseOcrService CreateService() =>
        new(_fixture.Factory, null!, null!, NullLogger<ExpenseOcrService>.Instance);

    private static string UniqueVatDigits() =>
        "9" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999).ToString();

    private async Task<int> InsertPartnerAsync(string name, string? vatCode, int? defaultCategoryId = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Supplier,
            Name = name,
            VatCode = vatCode,
            Country = "Lithuania",
            CountryCode = "LT",
            DefaultLanguage = "LT",
            PaymentTermDays = 14,
            DefaultVatRate = 21m,
            IsActive = true,
            DefaultExpenseCategoryId = defaultCategoryId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task DeletePartnersAsync(params int[] ids)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        foreach (var id in ids)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task EmptyVat_PartnerWithEmptyVatExists_ReturnsNull()
    {
        var emptyVatPartner = await InsertPartnerAsync($"EmptyVat {Guid.NewGuid():N}", "");
        try
        {
            var (supplierId, categoryId) = await CreateService()
                .FindSupplierIdAsync($"NoSuchSupplier {Guid.NewGuid():N}", "");

            Assert.Null(supplierId);
            Assert.Null(categoryId);
        }
        finally
        {
            await DeletePartnersAsync(emptyVatPartner);
        }
    }

    [Fact]
    public async Task VatWithLtPrefix_MatchesPartnerStoredWithoutPrefix()
    {
        var digits = UniqueVatDigits();
        var id = await InsertPartnerAsync($"VatNoPrefix {Guid.NewGuid():N}", digits);
        try
        {
            var (supplierId, _) = await CreateService().FindSupplierIdAsync("", "LT" + digits);
            Assert.Equal(id, supplierId);
        }
        finally
        {
            await DeletePartnersAsync(id);
        }
    }

    [Fact]
    public async Task VatWithoutPrefix_MatchesPartnerStoredWithLtPrefix()
    {
        var digits = UniqueVatDigits();
        var id = await InsertPartnerAsync($"VatPrefix {Guid.NewGuid():N}", "LT" + digits);
        try
        {
            var (supplierId, _) = await CreateService().FindSupplierIdAsync("", digits);
            Assert.Equal(id, supplierId);
        }
        finally
        {
            await DeletePartnersAsync(id);
        }
    }

    [Fact]
    public async Task PartialName_NoLongerMatches()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var id = await InsertPartnerAsync($"{prefix} Dezekspresas", null);
        try
        {
            var (supplierId, _) = await CreateService().FindSupplierIdAsync($"{prefix} Dezeks", "");
            Assert.Null(supplierId);
        }
        finally
        {
            await DeletePartnersAsync(id);
        }
    }

    [Fact]
    public async Task TwoPartnersWithSameExactName_ReturnsNull()
    {
        var name = $"Twin Supplier {Guid.NewGuid():N}";
        var a = await InsertPartnerAsync(name, null);
        var b = await InsertPartnerAsync(name, null);
        try
        {
            var (supplierId, categoryId) = await CreateService().FindSupplierIdAsync(name, "");
            Assert.Null(supplierId);
            Assert.Null(categoryId);
        }
        finally
        {
            await DeletePartnersAsync(a, b);
        }
    }

    [Fact]
    public async Task TwoPartnersMatchingSameNormalisedVat_ReturnsNull()
    {
        var digits = UniqueVatDigits();
        var a = await InsertPartnerAsync($"VatTwinA {Guid.NewGuid():N}", digits);
        var b = await InsertPartnerAsync($"VatTwinB {Guid.NewGuid():N}", "LT" + digits);
        try
        {
            var (supplierId, categoryId) = await CreateService().FindSupplierIdAsync("", "LT" + digits);
            Assert.Null(supplierId);
            Assert.Null(categoryId);
        }
        finally
        {
            await DeletePartnersAsync(a, b);
        }
    }

    [Fact]
    public async Task ExactVatMatch_ReturnsPartnerAndDefaultCategory()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var category = new NordicBeesERP.Models.Expenses.ExpenseCategory { Name = $"FindSupplierTest {Guid.NewGuid():N}"[..40], Code = "FST" + Random.Shared.Next(1000, 9999) };
        context.ExpenseCategories.Add(category);
        await context.SaveChangesAsync();

        var vat = "LT" + UniqueVatDigits();
        var id = await InsertPartnerAsync($"ExactVat {Guid.NewGuid():N}", vat, category.Id);
        try
        {
            // lower-case with a space: normalisation must still find it
            var ocrVat = vat.ToLowerInvariant().Insert(4, " ");
            var (supplierId, categoryId) = await CreateService().FindSupplierIdAsync("", ocrVat);

            Assert.Equal(id, supplierId);
            Assert.Equal(category.Id, categoryId);
        }
        finally
        {
            await DeletePartnersAsync(id);
            await context.Database.ExecuteSqlRawAsync("DELETE FROM expense_categories WHERE id = {0}", category.Id);
        }
    }
}
