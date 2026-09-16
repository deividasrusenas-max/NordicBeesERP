using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests for ExpenseService write methods against the real
/// nordic_bees_erp_test database. Verifies that ExecuteSqlRawAsync-based
/// UPDATE/DELETE actually persist changes (not silent NoTracking no-ops).
/// </summary>
public class ExpenseServiceTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ExpenseServiceTests(DbTestFixture fixture)
    {
        _fixture = fixture;
    }

    private ExpenseService CreateService()
    {
        return new ExpenseService(
            _fixture.Factory,
            new TestAuthService(),
            new TestCompanySettingsService());
    }

    /// <summary>
    /// Inserts a minimal expense_invoices row and returns its id.
    /// The invoice_number is guaranteed unique via Guid.
    /// </summary>
    private async Task<int> InsertTestInvoiceAsync(string? notes = null)
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var invoiceNumber = $"EXP-TEST-{Guid.NewGuid():N}";
        var invoiceDate = DateTime.UtcNow.Date;
        var dueDate = invoiceDate.AddDays(30);
        var amountExclVat = 100m;
        var vatRate = 21m;
        var vatAmount = 21m;
        var amountInclVat = 121m;

        // Notes is nullable — NULL in SQL when null in C#
        var notesParam = notes ?? "";

        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO expense_invoices " +
            "(invoice_number, invoice_date, due_date, amount_excl_vat, vat_rate, vat_amount, amount_incl_vat, notes, status, currency, source, ocr_status, created_at, updated_at) " +
            "VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, NOW(), NOW())",
            invoiceNumber,
            invoiceDate,
            dueDate,
            amountExclVat,
            vatRate,
            vatAmount,
            amountInclVat,
            notesParam,
            "DRAFT",
            "EUR",
            "MANUAL",
            "PENDING");

        // Read back the inserted id by matching on the unique invoice_number
        var id = await context.ExpenseInvoices
            .FromSqlRaw("SELECT * FROM expense_invoices WHERE invoice_number = {0}", invoiceNumber)
            .AsNoTracking()
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        return id;
    }

    [Fact]
    public async Task UpdateInvoiceAsync_PersistsChangesToRealDatabase()
    {
        var id = await InsertTestInvoiceAsync(notes: null);

        var service = CreateService();

        // Build the ExpenseInvoice object with modified values
        var invoice = new ExpenseInvoice
        {
            Id = id,
            InvoiceNumber = $"EXP-UPDATED-{Guid.NewGuid():N}",
            InvoiceDate = DateTime.UtcNow.Date,
            DueDate = DateTime.UtcNow.Date.AddDays(60),
            AmountExclVat = 200m,
            VatRate = 21m,
            VatAmount = 42m,
            AmountInclVat = 242m,
            Notes = "Updated notes from test",
            Status = "PENDING",
            UpdatedAt = DateTime.UtcNow,
        };

        await service.UpdateInvoiceAsync(invoice);

        // Verify via a completely fresh context that the write actually reached the DB
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated notes from test", reloaded!.Notes);
        Assert.Equal("PENDING", reloaded.Status);
        Assert.Equal(200m, reloaded.AmountExclVat);

        // Cleanup
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_RemovesInvoiceFromDatabase()
    {
        var id = await InsertTestInvoiceAsync();

        var service = CreateService();

        // Delete the invoice
        var result = await service.DeleteInvoiceAsync(id);
        Assert.True(result);

        // Verify row is actually gone via a fresh context
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var exists = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .AnyAsync(i => i.Id == id);

        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteInvoiceAsync_ReturnsFalseForNonExistentId()
    {
        var service = CreateService();

        var result = await service.DeleteInvoiceAsync(999999);
        Assert.False(result);
    }

    /// <summary>
    /// Inserts a minimal files row (same column list as FileStore.InsertFileRowAsync)
    /// and returns its id. entity_id is NULL — CreateFromOcrAsync backfills it.
    /// </summary>
    private async Task<long> InsertTestFileRowAsync()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        // LAST_INSERT_ID() is connection-scoped: hold one connection explicitly
        // open across the INSERT and the id read so both run on the same MySQL session.
        var conn = context.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs)
            await conn.OpenAsync();

        try
        {
            var sha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO files (sha256, byte_size, mime_type, original_filename, module, entity_type, entity_id, created_at, created_by) " +
                                  "VALUES (@p0, @p1, @p2, @p3, @p4, @p5, NULL, NOW(), 'TEST')";
                var p0 = cmd.CreateParameter();
                p0.ParameterName = "@p0";
                p0.Value = sha256;
                cmd.Parameters.Add(p0);
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@p1";
                p1.Value = 128L;
                cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@p2";
                p2.Value = "application/pdf";
                cmd.Parameters.Add(p2);
                var p3 = cmd.CreateParameter();
                p3.ParameterName = "@p3";
                p3.Value = "test-invoice.pdf";
                cmd.Parameters.Add(p3);
                var p4 = cmd.CreateParameter();
                p4.ParameterName = "@p4";
                p4.Value = "expenses";
                cmd.Parameters.Add(p4);
                var p5 = cmd.CreateParameter();
                p5.ParameterName = "@p5";
                p5.Value = "expense_invoice";
                cmd.Parameters.Add(p5);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var sel = conn.CreateCommand())
            {
                sel.CommandText = "SELECT LAST_INSERT_ID()";
                return Convert.ToInt32(await sel.ExecuteScalarAsync());
            }
        }
        finally
        {
            if (openedByUs)
                await conn.CloseAsync();
        }
    }

    private static OcrResultDto NewTestOcrResult(long? fileId) => new()
    {
        InvoiceNumber = $"OCR-TEST-{Guid.NewGuid():N}",
        InvoiceDate = DateTime.UtcNow.Date.ToString("yyyy-MM-dd"),
        DueDate = DateTime.UtcNow.Date.AddDays(30).ToString("yyyy-MM-dd"),
        Currency = "EUR",
        AmountExclVat = 100m,
        VatRate = 21m,
        VatAmount = 21m,
        AmountInclVat = 121m,
        SupplierId = null, // no lookup-table row needed; lands in PENDING_SUPPLIER
        SupplierName = "Test OCR Supplier",
        FileId = fileId,
        Confidence = new OcrConfidenceDto { Amounts = 90, InvoiceNumber = 90, SupplierName = 85, InvoiceDate = 80 }
    };

    [Fact]
    public async Task CreateFromOcrAsync_WithFileId_PersistsInvoiceFileIdAndBackfillsFilesEntityId()
    {
        var fileId = await InsertTestFileRowAsync();

        var service = CreateService();
        var ocrResult = NewTestOcrResult(fileId);
        ocrResult.RawJson = "{\"test\":\"c6-raw-json\"}";
        ocrResult.OcrPipeline = "prebuilt-invoice";
        var invoice = await service.CreateFromOcrAsync(ocrResult, "MANUAL");

        // Verify via a completely fresh context that both writes reached the DB
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

        var invoiceFileId = await verifyContext.Database
            .SqlQueryRaw<long>("SELECT file_id AS Value FROM expense_invoices WHERE id = {0}", invoice.Id)
            .SingleAsync();
        Assert.Equal(fileId, invoiceFileId);

        // C6: ocr_raw_json / ocr_pipeline persisted through the EF Add path
        var reloaded = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoice.Id);
        Assert.NotNull(reloaded);
        using var doc = JsonDocument.Parse(reloaded!.OcrRawJson!);
        Assert.Equal("c6-raw-json", doc.RootElement.GetProperty("test").GetString());
        Assert.Equal("prebuilt-invoice", reloaded.OcrPipeline);

        var entityIds = await verifyContext.Database
            .SqlQueryRaw<long>("SELECT entity_id AS Value FROM files WHERE id = {0}", fileId)
            .ToListAsync();
        Assert.Single(entityIds);
        Assert.Equal(invoice.Id, entityIds[0]);

        // Cleanup: audit rows first, then invoice, then the seeded file row
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoice.Id);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE id = {0}", invoice.Id);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM files WHERE id = {0}", fileId);
    }

    [Fact]
    public async Task CreateFromOcrAsync_NullFileId_SkipsBackfillWithoutError()
    {
        var service = CreateService();
        var invoice = await service.CreateFromOcrAsync(NewTestOcrResult(fileId: null), "MANUAL");

        // No exception above proves the backfill branch was skipped; confirm file_id is SQL NULL, not 0 or an empty string
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();

        // Explicitly open the connection: raw ADO.NET commands do not auto-open it.
        var conn = verifyContext.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs)
            await conn.OpenAsync();

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT file_id FROM expense_invoices WHERE id = @p0";
            var p = cmd.CreateParameter();
            p.ParameterName = "@p0";
            p.Value = invoice.Id;
            cmd.Parameters.Add(p);
            await using var reader = await cmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.IsDBNull(0));
        }
        finally
        {
            if (openedByUs)
                await conn.CloseAsync();
        }

        // Cleanup: audit rows first, then invoice
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoice_audit WHERE invoice_id = {0}", invoice.Id);
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE id = {0}", invoice.Id);
    }

    [Fact]
    public async Task ReOcrUpdate_PersistsRawJsonAndPipelineToRealDatabase()
    {
        var id = await InsertTestInvoiceAsync();

        var service = CreateService();
        var ocrResult = NewTestOcrResult(fileId: null);
        ocrResult.RawJson = "{\"test\":\"c6-reocr-raw-json\"}";
        ocrResult.OcrPipeline = "prebuilt-invoice";

        await service.UpdateFromOcrAsync((int)id, ocrResult);

        // Verify via a completely fresh context that the raw-SQL UPDATE actually wrote both columns
        await using var verifyContext = await _fixture.Factory.CreateDbContextAsync();
        var reloaded = await verifyContext.ExpenseInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        Assert.NotNull(reloaded);
        // ocr_raw_json is a MySQL json column — canonicalized on write, so compare semantically, not byte-for-byte
        using var doc = JsonDocument.Parse(reloaded!.OcrRawJson!);
        Assert.Equal("c6-reocr-raw-json", doc.RootElement.GetProperty("test").GetString());
        Assert.Equal("prebuilt-invoice", reloaded.OcrPipeline);

        // Cleanup
        await verifyContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM expense_invoices WHERE id = {0}", id);
    }

    // ============ Stub implementations ============

    private sealed class TestAuthService : IAuthService
    {
        public Task<ErpUser?> ValidateUserAsync(string email, string password) => Task.FromResult<ErpUser?>(null);
        public Task SeedAdminAsync(string email, string password) => Task.CompletedTask;
        public Task<ErpUser?> GetAuthenticatedUserAsync() => Task.FromResult<ErpUser?>(null);
        public Task<int?> GetCustomerIdAsync() => Task.FromResult<int?>(null);
        public Task<int?> GetUserIdAsync() => Task.FromResult<int?>(null);
        public Task<ErpUser?> GetUserByIdAsync(int userId) => Task.FromResult<ErpUser?>(null);
        public Task<string> GetRequiredActorNameAsync() => throw new NotImplementedException();
    }

    private sealed class TestCompanySettingsService : ICompanySettingsService
    {
        public Task<CompanySettings> GetSettingsAsync() => Task.FromResult(new CompanySettings());
        public Task UpdateSettingsAsync(CompanySettings settings) => Task.CompletedTask;
    }
}
