using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S5a — the VAT-code format gate in <see cref="ExpenseOcrService.ResolveSupplierAsync"/>
/// (PLAN-ETAPAS1 §1.2): a malformed code reaches neither VIES nor the VAT-code supplier match; the name
/// match may still run. Integration tests against nordic_bees_erp_test (supplier match reads partners).
/// </summary>
public class ExpenseOcrServiceVatFormatGateTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseOcrServiceVatFormatGateTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed class RecordingVies : IViesService
    {
        public List<string> Calls { get; } = new();

        public Task<ViesResult> LookupAsync(string vatCode)
        {
            Calls.Add(vatCode);
            return Task.FromResult(new ViesResult { ServiceAvailable = true, IsValid = false });
        }
    }

    private ExpenseOcrService CreateService(RecordingVies vies) =>
        new(_fixture.Factory, vies, null!, NullLogger<ExpenseOcrService>.Instance);

    private static string Digits(int count) =>
        string.Concat(Enumerable.Range(0, count).Select(_ => Random.Shared.Next(1, 10)));

    private async Task<int> InsertPartnerAsync(string name, string? vatCode)
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
            IsSupplier = true, // D-044 Q7: only active suppliers are matched automatically
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.BusinessPartners.Add(partner);
        await context.SaveChangesAsync();
        return partner.Id;
    }

    private async Task DeletePartnerAsync(int id)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", id);
    }

    [Fact]
    public async Task MalformedVat_SkipsVies_FlagsInvalidFormat_AndDoesNotMatchByVat()
    {
        var malformed = "LT" + Digits(5); // LT needs 9 or 12 digits
        var partnerId = await InsertPartnerAsync($"VatGate Partner {Guid.NewGuid():N}", malformed);
        try
        {
            var vies = new RecordingVies();
            var result = new OcrResultDto
            {
                SupplierName = $"Kitas Pavadinimas {Guid.NewGuid():N}",
                SupplierVatCode = malformed
            };

            await CreateService(vies).ResolveSupplierAsync(result, new CompanySettings());

            Assert.Empty(vies.Calls);
            Assert.Contains(OcrFlag.InvalidVatFormat, result.Flags);
            Assert.Null(result.SupplierId); // the partner has exactly this (malformed) code, but a code that fails format never matches
        }
        finally
        {
            await DeletePartnerAsync(partnerId);
        }
    }

    [Fact]
    public async Task MalformedVat_NameMatchStillRuns()
    {
        var malformed = "LT" + Digits(5);
        var name = $"VatGate NameMatch {Guid.NewGuid():N}";
        var partnerId = await InsertPartnerAsync(name, null);
        try
        {
            var vies = new RecordingVies();
            var result = new OcrResultDto { SupplierName = name, SupplierVatCode = malformed };

            await CreateService(vies).ResolveSupplierAsync(result, new CompanySettings());

            Assert.Empty(vies.Calls);
            Assert.Contains(OcrFlag.InvalidVatFormat, result.Flags);
            Assert.Equal(partnerId, result.SupplierId);
        }
        finally
        {
            await DeletePartnerAsync(partnerId);
        }
    }

    [Fact]
    public async Task AddressCountryIsTheHint_UnprefixedCodeOfWrongLengthIsMalformed()
    {
        // LV needs 11 digits; without a prefix the address country decides
        var vies = new RecordingVies();
        var result = new OcrResultDto
        {
            SupplierName = $"Latvija {Guid.NewGuid():N}",
            SupplierVatCode = Digits(9),
            SupplierCountryCode = "LV"
        };

        await CreateService(vies).ResolveSupplierAsync(result, new CompanySettings());

        Assert.Empty(vies.Calls);
        Assert.Contains(OcrFlag.InvalidVatFormat, result.Flags);
    }

    [Fact]
    public async Task WellFormedVat_CallsVies_NoFormatFlag()
    {
        var code = "LT" + Digits(9);
        var vies = new RecordingVies();
        var result = new OcrResultDto { SupplierName = $"Geras {Guid.NewGuid():N}", SupplierVatCode = code };

        await CreateService(vies).ResolveSupplierAsync(result, new CompanySettings());

        Assert.Equal(new[] { code }, vies.Calls);
        Assert.DoesNotContain(OcrFlag.InvalidVatFormat, result.Flags);
    }

    [Fact]
    public async Task WellFormedVat_StillMatchesSupplierByVat()
    {
        var code = "LT" + Digits(9);
        var partnerId = await InsertPartnerAsync($"VatGate ByVat {Guid.NewGuid():N}", code);
        try
        {
            var result = new OcrResultDto { SupplierName = $"Kitas {Guid.NewGuid():N}", SupplierVatCode = code };

            await CreateService(new RecordingVies()).ResolveSupplierAsync(result, new CompanySettings());

            Assert.Equal(partnerId, result.SupplierId);
        }
        finally
        {
            await DeletePartnerAsync(partnerId);
        }
    }

    [Theory]
    [InlineData("")]                 // no code: nothing to look up, nothing malformed
    [InlineData("GB123456789")]      // country outside the format table: not judged, still looked up
    public async Task EmptyOrUnknownCountryCode_NotGated(string code)
    {
        var vies = new RecordingVies();
        var result = new OcrResultDto { SupplierName = $"Nezinomas {Guid.NewGuid():N}", SupplierVatCode = code };

        await CreateService(vies).ResolveSupplierAsync(result, new CompanySettings());

        Assert.Equal(code.Length == 0 ? 0 : 1, vies.Calls.Count);
        Assert.DoesNotContain(OcrFlag.InvalidVatFormat, result.Flags);
    }
}
