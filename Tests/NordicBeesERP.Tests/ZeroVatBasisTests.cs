using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using UglyToad.PdfPig;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>VAT-02: 0 % legal-basis note on the invoice PDF, safe default rate for new customers, LT + 0 % warning rule.</summary>
public class ZeroVatBasisRuleTests
{
    [Theory]
    [InlineData("LT", "LT123456789", 21)]
    [InlineData("LT", null, 21)]
    [InlineData("", null, 21)]        // unknown country: never 0 %
    [InlineData(null, "LT123456789", 21)]
    [InlineData("LV", "LV40003000001", 0)]  // EU non-LT with VAT code
    [InlineData("DE", "DE123456789", 0)]
    [InlineData("LV", "", 21)]        // EU non-LT without VAT code
    [InlineData("LV", null, 21)]
    [InlineData("GB", null, 0)]       // outside EU
    [InlineData("NO", "NO123456789MVA", 0)]
    public void DefaultRateForNewCustomer_Matrix(string? country, string? vat, int expected) =>
        Assert.Equal((decimal)expected, ZeroVatBasis.DefaultRateForNewCustomer(country, vat));

    [Theory]
    [InlineData("DE", false, ZeroVatBasis.LtIntraCommunityNote)]
    [InlineData("DE", true, ZeroVatBasis.EnIntraCommunityNote)]
    [InlineData("GB", false, ZeroVatBasis.LtExportNote)]
    [InlineData("GB", true, ZeroVatBasis.EnExportNote)]
    [InlineData("LT", false, null)]
    [InlineData("", false, null)]
    public void NoteFor_ZeroLine_FollowsCountry(string country, bool english, string? expected) =>
        Assert.Equal(expected, ZeroVatBasis.NoteFor(country, null, new[] { 0m }, english));

    [Fact]
    public void NoteFor_NoZeroLine_IsNull() =>
        Assert.Null(ZeroVatBasis.NoteFor("DE", "DE1", new[] { 21m, 9m }, false));

    [Theory]
    [InlineData("LT", 0, true)]
    [InlineData("LT", 21, false)]
    [InlineData("DE", 0, false)]
    [InlineData("GB", 0, false)]
    public void Warning_OnlyForLtCustomerWithZeroRate(string country, int rate, bool expected) =>
        Assert.Equal(expected, ZeroVatBasis.ShouldWarnZeroVatToLtCustomer(country, null, new[] { (decimal)rate }));
}

[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class ZeroVatBasisPdfTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;
    public ZeroVatBasisPdfTests(DbTestFixture fixture) => _fixture = fixture;

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "wwwroot", "logo.png"))) return Path.Combine(dir.FullName, "wwwroot");
            dir = dir.Parent;
        }
        throw new InvalidOperationException("wwwroot not found");
    }

    private static string Text(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        return Regex.Replace(string.Join(" ", doc.GetPages().Select(p => p.Text)), @"\s+", "");
    }

    private async Task<(int Partner, int Invoice)> SeedAsync(string countryCode, string language, decimal rate)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        if (!await ctx.CompanySettings.AnyAsync())
        {
            ctx.CompanySettings.Add(new CompanySettings
            {
                CompanyName = "Test Company UAB", CompanyCode = "TC-TEST-001", VatCode = "LT000000001", Address = "Test street 1",
                City = "Vilnius", PostalCode = "12345", Country = "Lietuva", CountryCode = "LT", BankName = "Test Bank",
                BankIban = "LT000000000000000001", BankSwift = "TESTLT2X", BankAccount = "123456", Email = "test@example.com",
                Phone = "+370 123 45678", DefaultVatRate = 21m, UpdatedAt = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer, IsCustomer = true, Name = $"Zero VAT Customer {Guid.NewGuid():N}", CompanyCode = "C-1",
            VatCode = countryCode + "000000099", Address = "Street 5", City = "City", PostalCode = "1", Country = countryCode,
            CountryCode = countryCode, DefaultLanguage = language, PaymentTermDays = 14, DefaultVatRate = rate, IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.BusinessPartners.Add(partner);
        await ctx.SaveChangesAsync();
        var invoice = new Invoice
        {
            InvoiceNumber = $"INV-ZV-{Guid.NewGuid():N}", InvoiceDate = DateTime.UtcNow.Date, CustomerId = partner.Id, Language = language,
            InvoiceType = "PVM SĄSKAITA FAKTŪRA", Status = InvoiceStatus.Draft, PaymentDueDate = DateTime.UtcNow.Date.AddDays(14),
            SubtotalExclVat = 100m, TotalVat = rate == 0 ? 0m : 21m, TotalInclVat = rate == 0 ? 100m : 121m,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        ctx.Invoices.Add(invoice);
        await ctx.SaveChangesAsync();
        ctx.Set<InvoiceLine>().Add(new InvoiceLine
        {
            InvoiceId = invoice.Id, LineNumber = 1, Description = "Honey", Unit = "kg", Quantity = 1m, PriceExclVat = 100m,
            VatRate = rate, LineSubtotal = 100m, VatAmount = rate == 0 ? 0m : 21m, LineTotal = rate == 0 ? 100m : 121m
        });
        await ctx.SaveChangesAsync();
        return (partner.Id, invoice.Id);
    }

    private async Task CleanupAsync((int Partner, int Invoice) s)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", s.Invoice);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", s.Invoice);
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", s.Partner);
    }

    private string Render(int invoiceId)
    {
        var env = new Env { WebRootPath = WebRoot() };
        var service = new PdfGeneratorService(_fixture.Factory, new CompanySettingsService(_fixture.Factory), env);
        return Text(service.GenerateInvoicePdf(invoiceId));
    }

    // PdfPig drops some 't' glyphs from the QuestPDF font subset, so only digit-bearing, t-free fragments are asserted.
    [Theory]
    [InlineData("DE", "LT", 0, "49str.", "41str.")]
    [InlineData("GB", "LT", 0, "41str.", "49str.")]
    [InlineData("DE", "EN", 0, "Art.138", "Art.41")]
    [InlineData("GB", "EN", 0, "Art.41", "Art.138")]
    public async Task Pdf_ZeroRateLine_ShowsLegalBasisForCountry(string country, string lang, int rate, string present, string absent)
    {
        var s = await SeedAsync(country, lang, rate);
        try
        {
            var text = Render(s.Invoice);
            Assert.Contains(present, text);
            Assert.DoesNotContain(absent, text);
        }
        finally { await CleanupAsync(s); }
    }

    [Theory]
    [InlineData("DE", "LT")]
    [InlineData("LT", "LT")]
    public async Task Pdf_21PercentOrLtCustomer_HasNoLegalBasisNote(string country, string lang)
    {
        var s = await SeedAsync(country, lang, country == "LT" ? 0 : 21);
        try
        {
            var text = Render(s.Invoice);
            Assert.DoesNotContain("49str.", text);
            Assert.DoesNotContain("41str.", text);
        }
        finally { await CleanupAsync(s); }
    }

    private sealed class Env : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
