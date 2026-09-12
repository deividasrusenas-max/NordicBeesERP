using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using NordicBeesERP.Data;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using UglyToad.PdfPig;
using Xunit;

namespace NordicBeesERP.Tests;

public class PdfGeneratorServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public PdfGeneratorServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    // Verbatim strings from the committed C1 implementation in Services/PdfGeneratorService.cs —
    // these assertions must match the production labels EXACTLY (lithuanian/English coherence gate).
    private const string Rc96Note = "Taikomas atvirkštinio apmokestinimo PVM mechanizmas, pagal 96 str.";
    // PdfPig text extraction drops certain 't' glyphs from the embedded QuestPDF font subset
    // (e.g. "atvirkštinio" -> "atvirkšnio"), so the full note cannot be matched verbatim in
    // extracted text. "mechanizmas" is unique to the RC96 note and has no extraction-fragile glyph.
    private const string Rc96NoteStableFragment = "mechanizmas";
    private const string AmountPayableLabel = "Suma apmokėjimui:";
    private const string OutputDir = "/tmp/rc96-verification";

    // ---------------------------------------------------------------
    // Infrastructure
    // ---------------------------------------------------------------

    private static string ResolveRepoWebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "wwwroot", "logo.png")))
                return Path.Combine(dir.FullName, "wwwroot");
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Could not locate repo wwwroot from {AppContext.BaseDirectory}");
    }

    private PdfGeneratorService CreateService()
    {
        var env = new StubWebHostEnvironment { WebRootPath = ResolveRepoWebRoot() };
        var companySettings = new CompanySettingsService(_fixture.Factory);
        return new PdfGeneratorService(_fixture.Factory, companySettings, env);
    }

    private static string ExtractNormalizedText(byte[] pdf)
    {
        using var doc = PdfDocument.Open(pdf);
        var text = string.Join(" ", doc.GetPages().Select(p => p.Text));
        return Regex.Replace(text, @"\s+", " ");
    }

    private static string NoSpaces(string s) => s.Replace(" ", "");

    private async Task<(bool Created, int SettingsId)> EnsureCompanySettingsAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var existing = await context.CompanySettings.FirstOrDefaultAsync();
        if (existing != null)
            return (false, existing.Id);

        var settings = new CompanySettings
        {
            CompanyName = "Test Company UAB",
            CompanyCode = "TC-TEST-001",
            VatCode = "LT000000001",
            Address = "Test street 1",
            City = "Vilnius",
            PostalCode = "12345",
            Country = "Lietuva",
            CountryCode = "LT",
            BankName = "Test Bank",
            BankIban = "LT000000000000000001",
            BankSwift = "TESTLT2X",
            BankAccount = "123456",
            Email = "test@example.com",
            Phone = "+370 123 45678",
            DefaultVatRate = 21m,
            UpdatedAt = DateTime.UtcNow
        };
        context.CompanySettings.Add(settings);
        await context.SaveChangesAsync();
        return (true, settings.Id);
    }

    private async Task<int> SeedCustomerAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var partner = new BusinessPartner
        {
            PartnerType = PartnerType.Customer,
            IsCustomer = true,
            Name = $"PDF Test Customer {Guid.NewGuid():N}",
            CompanyCode = "CUST-TEST-001",
            VatCode = "LT000000099",
            Address = "Customer street 5",
            City = "Kaunas",
            PostalCode = "44001",
            Phone = "+370 600 00001",
            Email = "customer@example.com",
            BankAccount = "123456789012",
            NationalIdNumber = "123456789",
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
        return partner.Id;
    }

    private async Task CleanupAsync(int partnerId, int invoiceId, bool settingsCreated, int settingsId)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM invoice_lines WHERE invoice_id = {0}", invoiceId);
        if (invoiceId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM invoices WHERE id = {0}", invoiceId);
        if (partnerId > 0)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM business_partners WHERE id = {0}", partnerId);
        if (settingsCreated)
            await context.Database.ExecuteSqlRawAsync("DELETE FROM company_settings WHERE id = {0}", settingsId);
    }

    // ---------------------------------------------------------------
    // RC96: 660 × 0.10 @ 21% — stored line 66/0/66, PDF must show 13.86 / 79.86 and "Suma apmokėjimui"
    // ---------------------------------------------------------------

    [Fact]
    public async Task GenerateInvoicePdf_ReverseCharge96_ShowsDisplayVatAndAmountPayable()
    {
        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedCustomerAsync();

            await using (var context = await _fixture.Factory.CreateDbContextAsync())
            {
                var invoiceDate = DateTime.UtcNow.Date;
                var invoice = new Invoice
                {
                    InvoiceNumber = $"INV-RC96-{Guid.NewGuid():N}",
                    InvoiceDate = invoiceDate,
                    CustomerId = partnerId,
                    Language = "LT",
                    InvoiceType = InvoiceTypes.ReverseCharge96,
                    Status = InvoiceStatus.Draft,
                    ReverseCharge = true,
                    PaymentDueDate = invoiceDate.AddDays(14),
                    SubtotalExclVat = 66m,
                    TotalVat = 0m,
                    TotalInclVat = 66m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Invoices.Add(invoice);
                await context.SaveChangesAsync();
                invoiceId = invoice.Id;

                context.Set<InvoiceLine>().Add(new InvoiceLine
                {
                    InvoiceId = invoiceId,
                    LineNumber = 1,
                    Description = "RC96 PDF verification line",
                    Unit = "kg",
                    Quantity = 0.10m,
                    PriceExclVat = 660m,
                    VatRate = 21m,
                    LineSubtotal = 66m,
                    VatAmount = 0m,
                    LineTotal = 66m
                });
                await context.SaveChangesAsync();
            }

            Directory.CreateDirectory(OutputDir);
            var service = CreateService();
            var pdf = service.GenerateInvoicePdf(invoiceId);
            File.WriteAllBytes(Path.Combine(OutputDir, "rc96-invoice.pdf"), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            Assert.Contains("21%", text);
            Assert.Contains("13.86", text);
            Assert.Contains("79.86", text);
            Assert.Contains("66.00", text);
            Assert.Contains(AmountPayableLabel, text);
            Assert.Contains(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
        }
        finally
        {
            await CleanupAsync(partnerId, invoiceId, settingsCreated, settingsId);
        }
    }

    // ---------------------------------------------------------------
    // ULAK 6%: 100 × 1.00 @ 6% — real VAT 6.00, total 106.00; must NOT show RC96 labels
    // (regression guard for the C1 flag split: Contains("6%") must stay on the 6% path)
    // ---------------------------------------------------------------

    [Fact]
    public async Task GenerateInvoicePdf_Ulak6_KeepsRealVatAndHasNoRc96Labels()
    {
        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedCustomerAsync();

            await using (var context = await _fixture.Factory.CreateDbContextAsync())
            {
                var invoiceDate = DateTime.UtcNow.Date;
                var invoice = new Invoice
                {
                    InvoiceNumber = $"INV-ULAK-{Guid.NewGuid():N}",
                    InvoiceDate = invoiceDate,
                    CustomerId = partnerId,
                    Language = "LT",
                    InvoiceType = InvoiceTypes.Ulak6,
                    Status = InvoiceStatus.Draft,
                    ReverseCharge = true,
                    PaymentDueDate = invoiceDate.AddDays(14),
                    SubtotalExclVat = 100m,
                    TotalVat = 6m,
                    TotalInclVat = 106m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Invoices.Add(invoice);
                await context.SaveChangesAsync();
                invoiceId = invoice.Id;

                context.Set<InvoiceLine>().Add(new InvoiceLine
                {
                    InvoiceId = invoiceId,
                    LineNumber = 1,
                    Description = "ULAK 6% verification line",
                    Unit = "kg",
                    Quantity = 1m,
                    PriceExclVat = 100m,
                    VatRate = 6m,
                    LineSubtotal = 100m,
                    VatAmount = 6m,
                    LineTotal = 106m
                });
                await context.SaveChangesAsync();
            }

            Directory.CreateDirectory(OutputDir);
            var service = CreateService();
            var pdf = service.GenerateInvoicePdf(invoiceId);
            File.WriteAllBytes(Path.Combine(OutputDir, "ulak6-invoice.pdf"), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            Assert.Contains("6%", text);
            Assert.Contains("100.00", text);
            Assert.Contains("6.00", text);
            Assert.Contains("106.00", text);
            Assert.DoesNotContain(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
            Assert.DoesNotContain(AmountPayableLabel, text);
        }
        finally
        {
            await CleanupAsync(partnerId, invoiceId, settingsCreated, settingsId);
        }
    }

    // ---------------------------------------------------------------
    // Standard 21%: 2 × 50 @ 21% — real VAT 21.00, total 121.00; must NOT show RC96 labels
    // ---------------------------------------------------------------

    [Fact]
    public async Task GenerateInvoicePdf_Standard_ShowsRealVatAndHasNoRc96Labels()
    {
        var (settingsCreated, settingsId) = await EnsureCompanySettingsAsync();
        int partnerId = 0;
        int invoiceId = 0;
        try
        {
            partnerId = await SeedCustomerAsync();

            await using (var context = await _fixture.Factory.CreateDbContextAsync())
            {
                var invoiceDate = DateTime.UtcNow.Date;
                var invoice = new Invoice
                {
                    InvoiceNumber = $"INV-STD-{Guid.NewGuid():N}",
                    InvoiceDate = invoiceDate,
                    CustomerId = partnerId,
                    Language = "LT",
                    InvoiceType = InvoiceTypes.Standard,
                    Status = InvoiceStatus.Draft,
                    ReverseCharge = false,
                    PaymentDueDate = invoiceDate.AddDays(14),
                    SubtotalExclVat = 100m,
                    TotalVat = 21m,
                    TotalInclVat = 121m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Invoices.Add(invoice);
                await context.SaveChangesAsync();
                invoiceId = invoice.Id;

                context.Set<InvoiceLine>().Add(new InvoiceLine
                {
                    InvoiceId = invoiceId,
                    LineNumber = 1,
                    Description = "Standard invoice verification line",
                    Unit = "kg",
                    Quantity = 2m,
                    PriceExclVat = 50m,
                    VatRate = 21m,
                    LineSubtotal = 100m,
                    VatAmount = 21m,
                    LineTotal = 121m
                });
                await context.SaveChangesAsync();
            }

            Directory.CreateDirectory(OutputDir);
            var service = CreateService();
            var pdf = service.GenerateInvoicePdf(invoiceId);
            File.WriteAllBytes(Path.Combine(OutputDir, "standard-invoice.pdf"), pdf);

            Assert.True(pdf.Length >= 4, "PDF bytes too short");
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

            var text = ExtractNormalizedText(pdf);
            Assert.Contains("21%", text);
            Assert.Contains("100.00", text);
            Assert.Contains("21.00", text);
            Assert.Contains("121.00", text);
            Assert.DoesNotContain(NoSpaces(Rc96NoteStableFragment), NoSpaces(text));
            Assert.DoesNotContain(AmountPayableLabel, text);
        }
        finally
        {
            await CleanupAsync(partnerId, invoiceId, settingsCreated, settingsId);
        }
    }

    // ---------------------------------------------------------------
    // IWebHostEnvironment stub — only WebRootPath is used by PdfGeneratorService
    // ---------------------------------------------------------------

    private sealed class StubWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
    }
}
