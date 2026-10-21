using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NordicBeesERP.Data;
using NordicBeesERP.Services.Dtos;

namespace NordicBeesERP.Services
{
    public class OcrQueueWorker : BackgroundService
    {
        private readonly ILogger<OcrQueueWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

        public OcrQueueWorker(
            ILogger<OcrQueueWorker> logger,
            IServiceScopeFactory scopeFactory,
            IDbContextFactory<NordicBeesERPContext> dbFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _dbFactory = dbFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Queue Worker starting at {Time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var ocrService = scope.ServiceProvider.GetRequiredService<IExpenseOcrService>();

                    if (!await ocrService.IsAzureHealthyAsync())
                    {
                        _logger.LogWarning("Azure DI not configured or unreachable. Skipping OCR processing.");
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                        continue;
                    }

                    await ProcessQueueItemAsync(ocrService, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in OCR Queue Worker iteration");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            _logger.LogInformation("OCR Queue Worker stopping at {Time}", DateTimeOffset.Now);
        }

        private async Task ProcessQueueItemAsync(IExpenseOcrService ocrService, CancellationToken cancellationToken)
        {
            using var context = _dbFactory.CreateDbContext();

            var queueItem = await context.ExpenseOcrQueue
                .Where(q => q.Status == "WAITING" && q.Attempts < q.MaxAttempts)
                .OrderBy(q => q.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (queueItem == null)
            {
                _logger.LogInformation("No queue items to process");
                return;
            }

            _logger.LogInformation("Processing queue item {QueueItemId}", queueItem.Id);

            // NoTracking is the global default: mutating queueItem/invoice and calling SaveChangesAsync persisted
            // nothing, so the same file was re-OCR'd every 30 s. Every write below is an explicit UPDATE.
            queueItem.Attempts++;
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE expense_ocr_queue SET status = 'PROCESSING', attempts = {0} WHERE id = {1}",
                new object[] { queueItem.Attempts, queueItem.Id }, cancellationToken);

            var ocrResult = await ocrService.ProcessAsync(queueItem.FileContent, queueItem.FileName);

            if (ocrResult.Success)
            {
                if (queueItem.InvoiceId.HasValue)
                {
                    var invoice = await context.ExpenseInvoices
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.Id == queueItem.InvoiceId.Value, cancellationToken);

                    if (invoice != null)
                    {
                        var invoiceNumber = !string.IsNullOrEmpty(ocrResult.InvoiceNumber) ? ocrResult.InvoiceNumber : invoice.InvoiceNumber;
                        var invoiceDate = invoice.InvoiceDate;
                        if (!string.IsNullOrEmpty(ocrResult.InvoiceDate) && DateTime.TryParse(ocrResult.InvoiceDate, out var parsedInvoiceDate))
                            invoiceDate = parsedInvoiceDate;
                        var dueDate = invoice.DueDate;
                        if (!string.IsNullOrEmpty(ocrResult.DueDate) && DateTime.TryParse(ocrResult.DueDate, out var parsedDueDate))
                            dueDate = parsedDueDate;
                        var currency = !string.IsNullOrEmpty(ocrResult.Currency) ? ocrResult.Currency : invoice.Currency;

                        int? supplierId = invoice.SupplierId;
                        string? pendingName = invoice.PendingSupplierName;
                        string? pendingVat = invoice.PendingSupplierVat;
                        string? pendingAddress = invoice.PendingSupplierAddress;

                        if (!string.IsNullOrEmpty(ocrResult.SupplierVatCode))
                        {
                            var supplier = await context.BusinessPartners
                                .AsNoTracking()
                                .FirstOrDefaultAsync(sp => sp.VatCode == ocrResult.SupplierVatCode && sp.IsActive, cancellationToken);
                            if (supplier != null)
                            {
                                supplierId = supplier.Id;
                                pendingName = null;
                                pendingVat = null;
                                pendingAddress = null;
                            }
                            else
                            {
                                pendingName = ocrResult.SupplierName;
                                pendingVat = ocrResult.SupplierVatCode;
                                pendingAddress = null;
                            }
                        }
                        else if (!string.IsNullOrEmpty(ocrResult.SupplierName))
                        {
                            pendingName = ocrResult.SupplierName;
                            pendingVat = null;
                            pendingAddress = null;
                        }

                        // paid_amount is deliberately NOT written: OCR reading an invoice must never mark it paid.
                        await context.Database.ExecuteSqlRawAsync(
                            "UPDATE expense_invoices SET ocr_status = 'COMPLETED', ocr_confidence = 95, invoice_number = {0}, invoice_date = {1}, due_date = {2}, currency = {3}, amount_excl_vat = {4}, vat_rate = {5}, vat_amount = {6}, amount_incl_vat = {7}, supplier_id = {8}, pending_supplier_name = {9}, pending_supplier_vat = {10}, pending_supplier_address = {11} WHERE id = {12}",
                            new object?[]
                            {
                                invoiceNumber, invoiceDate, dueDate, currency,
                                ocrResult.AmountExclVat, ocrResult.VatRate, ocrResult.VatAmount, ocrResult.AmountInclVat,
                                supplierId, pendingName, pendingVat, pendingAddress, invoice.Id
                            },
                            cancellationToken);
                    }
                }

                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE expense_ocr_queue SET status = 'COMPLETED', processed_at = {0} WHERE id = {1}",
                    new object[] { DateTime.UtcNow, queueItem.Id }, cancellationToken);
                _logger.LogInformation("Successfully processed queue item {QueueItemId}", queueItem.Id);
            }
            else
            {
                if (queueItem.Attempts >= queueItem.MaxAttempts)
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE expense_ocr_queue SET status = 'FAILED', error_message = {0} WHERE id = {1}",
                        new object?[] { ocrResult.Diagnostics.AzureError ?? "Unknown error", queueItem.Id }, cancellationToken);
                    _logger.LogError("Queue item {QueueItemId} failed after {Attempts} attempts", queueItem.Id, queueItem.Attempts);
                }
                else
                {
                    await context.Database.ExecuteSqlRawAsync(
                        "UPDATE expense_ocr_queue SET status = 'WAITING' WHERE id = {0}",
                        new object[] { queueItem.Id }, cancellationToken);
                    _logger.LogInformation("Queue item {QueueItemId} will be retried", queueItem.Id);
                }
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Queue Worker stopping at {Time}", DateTimeOffset.Now);
            await base.StopAsync(stoppingToken);
        }
    }
}
