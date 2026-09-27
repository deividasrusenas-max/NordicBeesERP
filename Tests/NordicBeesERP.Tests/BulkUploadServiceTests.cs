using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NordicBeesERP.Components.Pages.Admin;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;
using NordicBeesERP.Services.Storage;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-048 item 1 (Part B): proves BulkUploadService runs the SAME pipeline as the upload dialog
/// (PdfIntakeCheck, SHA-256 dedup, the OCR service, FileStore, ExpenseService.CreateFromOcrAsync)
/// and that a batch continues past a refused/failed file. Real ExpenseService/FileStore against
/// the real nordic_bees_erp_test database (same pattern as CreditNoteServiceTests/FileStoreTests);
/// only the OCR call itself is faked, so no test ever calls real Azure.
/// </summary>
[Collection("RealDatabase")]
public class BulkUploadServiceTests : IClassFixture<DbTestFixture>, IDisposable
{
    private readonly DbTestFixture _fixture;
    private readonly string _blobRoot;
    private readonly string _marker;

    static BulkUploadServiceTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public BulkUploadServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
        _blobRoot = Path.Combine(Path.GetTempPath(), "nordicbees-bulkupload-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_blobRoot);
        _marker = $"BULKTEST-{DateTime.UtcNow.Ticks}";
    }

    public void Dispose()
    {
        try
        {
            using var db = _fixture.Factory.CreateDbContext();
            db.Database.ExecuteSqlRaw(
                "DELETE FROM expense_invoice_audit WHERE invoice_number LIKE {0}", _marker + "%");
            db.Database.ExecuteSqlRaw(
                "DELETE FROM expense_invoice_lines WHERE invoice_id IN (SELECT id FROM (SELECT id FROM expense_invoices WHERE invoice_number LIKE {0}) t)", _marker + "%");
            db.Database.ExecuteSqlRaw(
                "DELETE FROM expense_invoices WHERE invoice_number LIKE {0}", _marker + "%");
            db.Database.ExecuteSqlRaw(
                "DELETE FROM files WHERE original_filename LIKE {0}", "%" + _marker + "%");

            if (Directory.Exists(_blobRoot)) Directory.Delete(_blobRoot, true);
        }
        catch
        {
            // Best-effort cleanup: a failed teardown must not mask the real test result.
        }
    }

    private static byte[] ValidDigitalPdf(string invoiceNumber) =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(595, 842);
            p.Content().Text($"Invoice {invoiceNumber} total 121.00 EUR supplier Test Bulk Supplier Ltd");
        })).GeneratePdf();

    private BulkUploadService NewService(IExpenseOcrService ocrService) => new(
        ocrService,
        new ExpenseService(_fixture.Factory, new NullAuthService(), new DefaultCompanySettingsService()),
        new FileStore(Options.Create(new FileStorageOptions { Root = _blobRoot }), _fixture.Factory),
        _fixture.Factory,
        NullLogger<BulkUploadService>.Instance);

    private OcrResultDto ValidOcrResult(string invoiceNumber) => new()
    {
        InvoiceNumber = invoiceNumber,
        InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
        DueDate = DateTime.Today.AddDays(14).ToString("yyyy-MM-dd"),
        AmountExclVat = 100.00m,
        VatRate = 21.0m,
        VatAmount = 21.00m,
        AmountInclVat = 121.00m,
        SupplierName = "Test Bulk Supplier Ltd",
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 90, InvoiceDate = 90 }
    };

    [Fact]
    public async Task ProcessFileAsync_ValidDigitalPdf_CreatesInvoiceWithBulkCreatedAudit()
    {
        var invoiceNumber = $"{_marker}-OK";
        var batchId = Guid.NewGuid();
        var ocr = new FakeOcrService(ValidOcrResult(invoiceNumber));
        var service = NewService(ocr);

        var result = await service.ProcessFileAsync(ValidDigitalPdf(invoiceNumber), $"{invoiceNumber}.pdf", batchId);

        Assert.True(result.Accepted);
        Assert.NotNull(result.InvoiceId);
        Assert.Equal(1, ocr.CallCount);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var invoiceCount = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM expense_invoices WHERE id = {0}", result.InvoiceId!.Value).FirstAsync();
        Assert.Equal(1, invoiceCount);

        var actions = await verify.Database.SqlQueryRaw<string>(
            "SELECT action AS Value FROM expense_invoice_audit WHERE invoice_id = {0} ORDER BY id", result.InvoiceId!.Value)
            .ToListAsync();
        Assert.Contains("CREATED", actions);
        Assert.Contains("BULK_CREATED", actions);

        var bulkDetails = await verify.Database.SqlQueryRaw<string>(
            "SELECT action_details AS Value FROM expense_invoice_audit WHERE invoice_id = {0} AND action = 'BULK_CREATED'", result.InvoiceId!.Value)
            .FirstAsync();
        Assert.Contains(batchId.ToString(), bulkDetails);
    }

    [Fact]
    public async Task ProcessFileAsync_NotAPdf_RefusesWithoutCallingOcrOrCreatingAnything()
    {
        var ocr = new FakeOcrService(ValidOcrResult($"{_marker}-NOTUSED"));
        var service = NewService(ocr);
        var plainTextBytes = System.Text.Encoding.UTF8.GetBytes("this is not a pdf");

        var result = await service.ProcessFileAsync(plainTextBytes, $"{_marker}-notpdf.pdf", Guid.NewGuid());

        Assert.False(result.Accepted);
        Assert.NotNull(result.RefusalReason);
        Assert.Null(result.InvoiceId);
        Assert.Equal(0, ocr.CallCount); // never reaches Azure

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var count = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM expense_invoices WHERE invoice_number LIKE {0}", _marker + "%").FirstAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ProcessFileAsync_DuplicateFile_RefusesSecondUploadOfSameBytes()
    {
        var invoiceNumber = $"{_marker}-DUP";
        var pdfBytes = ValidDigitalPdf(invoiceNumber);
        var ocr = new FakeOcrService(ValidOcrResult(invoiceNumber));
        var service = NewService(ocr);
        var batchId = Guid.NewGuid();

        var first = await service.ProcessFileAsync(pdfBytes, $"{invoiceNumber}.pdf", batchId);
        Assert.True(first.Accepted);

        var second = await service.ProcessFileAsync(pdfBytes, $"{invoiceNumber}-again.pdf", batchId);

        Assert.False(second.Accepted);
        Assert.Contains("jau įkeltas", second.RefusalReason);
        Assert.Equal(1, ocr.CallCount); // the duplicate check runs before the second OCR call
    }

    [Fact]
    public async Task ProcessFileAsync_OcrFailure_RefusesButBatchContinuesToNextFile()
    {
        var failedResult = new OcrResultDto
        {
            InvoiceNumber = "",
            AmountInclVat = 0,
            SupplierName = "",
            Diagnostics = new OcrDiagnosticsDto { AzureError = "simulated Azure timeout" }
        };
        var okInvoiceNumber = $"{_marker}-AFTERFAIL";
        var ocr = new FakeOcrService(failedResult, ValidOcrResult(okInvoiceNumber));
        var service = NewService(ocr);
        var batchId = Guid.NewGuid();

        var failedFileResult = await service.ProcessFileAsync(
            ValidDigitalPdf($"{_marker}-WILLFAIL"), $"{_marker}-willfail.pdf", batchId);

        Assert.False(failedFileResult.Accepted);
        Assert.Contains("OCR nepavyko", failedFileResult.RefusalReason);
        Assert.Null(failedFileResult.InvoiceId);

        // The batch continues: a second, independent file after a failure still succeeds.
        var okFileResult = await service.ProcessFileAsync(
            ValidDigitalPdf(okInvoiceNumber), $"{okInvoiceNumber}.pdf", batchId);

        Assert.True(okFileResult.Accepted);
        Assert.NotNull(okFileResult.InvoiceId);
        Assert.Equal(2, ocr.CallCount);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var failedCount = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM expense_invoices WHERE invoice_number = {0}", $"{_marker}-WILLFAIL").FirstAsync();
        Assert.Equal(0, failedCount);
    }

    /// <summary>Reviewer finding (Part B, first review): a partial-OCR outcome — amount and
    /// supplier extracted fine, invoice number not — used to sail past the OCR-failure gate,
    /// reach <see cref="IFileStore.SaveAsync"/> (persisting a real, unlinked <c>files</c> row +
    /// blob), and only then have <c>ExpenseService.CreateFromOcrAsync</c> throw on the blank
    /// invoice number — orphaning that row/blob forever, since <c>FindLinkedEntityIdsAsync</c>
    /// only matches rows with <c>entity_id</c> set. This proves the fix: refused before any file
    /// is ever saved, and no <c>files</c> row exists afterward.</summary>
    [Fact]
    public async Task ProcessFileAsync_ValidAmountAndSupplier_ButNoInvoiceNumber_RefusesBeforeSavingAnyFile()
    {
        var partialResult = new OcrResultDto
        {
            InvoiceNumber = "",
            InvoiceDate = DateTime.Today.ToString("yyyy-MM-dd"),
            AmountExclVat = 100.00m,
            VatAmount = 21.00m,
            AmountInclVat = 121.00m,
            SupplierName = "Test Bulk Supplier Ltd"
        };
        var ocr = new FakeOcrService(partialResult);
        var service = NewService(ocr);
        var fileName = $"{_marker}-NOINVNO.pdf";

        var result = await service.ProcessFileAsync(ValidDigitalPdf($"{_marker}-NOINVNO"), fileName, Guid.NewGuid());

        Assert.False(result.Accepted);
        Assert.Contains("numeris neatpažintas", result.RefusalReason);
        Assert.Null(result.InvoiceId);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var orphanedFileCount = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM files WHERE original_filename LIKE {0}", "%" + _marker + "-NOINVNO%").FirstAsync();
        Assert.Equal(0, orphanedFileCount);
    }

    [Fact]
    public void BulkUploadPage_RequiresAdminRole()
    {
        var authorizeAttribute = typeof(ExpenseBulkUpload)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .SingleOrDefault();

        Assert.NotNull(authorizeAttribute);
        Assert.Equal("Admin", authorizeAttribute!.Roles);
    }

    /// <summary>Tracks call order/count to prove sequential, one-Azure-call-at-a-time processing
    /// and that a batch continues after a refusal — no new mocking package, a hand-rolled fake
    /// matching this project's own established NullAuthService/DefaultCompanySettingsService
    /// convention (see ExpenseQuarantineTests etc.).</summary>
    private sealed class FakeOcrService : IExpenseOcrService
    {
        private readonly Queue<OcrResultDto> _results;
        public int CallCount { get; private set; }

        public FakeOcrService(params OcrResultDto[] results)
        {
            _results = new Queue<OcrResultDto>(results);
        }

        public Task<OcrResultDto> ProcessAsync(string base64, string fileName)
        {
            CallCount++;
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : new OcrResultDto());
        }

        public Task<OcrResultDto> ExtractInvoiceDataAsync(string base64, string fileName) => ProcessAsync(base64, fileName);

        public Task<(int? supplierId, int? defaultCategoryId)> FindSupplierIdAsync(string supplierName, string vatCode) =>
            Task.FromResult<(int?, int?)>((null, null));

        public Task<bool> IsAzureHealthyAsync() => Task.FromResult(true);
    }

    private sealed class NullAuthService : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => Task.FromResult("BULK_UPLOAD_TEST");
    }

    private sealed class DefaultCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
