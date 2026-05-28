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

            queueItem.Status = "PROCESSING";
            queueItem.Attempts++;
            await context.SaveChangesAsync(cancellationToken);

            var ocrResult = await ocrService.ProcessAsync(queueItem.FileContent, queueItem.FileName);

            if (ocrResult.Success)
            {
                queueItem.Status = "COMPLETED";
                queueItem.ProcessedAt = DateTime.UtcNow;

                if (queueItem.InvoiceId.HasValue)
                {
                    var invoice = await context.ExpenseInvoices
                        .FirstOrDefaultAsync(i => i.Id == queueItem.InvoiceId.Value, cancellationToken);

                    if (invoice != null)
                    {
                        invoice.OcrStatus = "COMPLETED";
                        invoice.OcrConfidence = 95;

                        if (!string.IsNullOrEmpty(ocrResult.InvoiceNumber))
                            invoice.InvoiceNumber = ocrResult.InvoiceNumber;

                        if (!string.IsNullOrEmpty(ocrResult.InvoiceDate) && DateTime.TryParse(ocrResult.InvoiceDate, out var invoiceDate))
                            invoice.InvoiceDate = invoiceDate;

                        if (!string.IsNullOrEmpty(ocrResult.DueDate) && DateTime.TryParse(ocrResult.DueDate, out var dueDate))
                            invoice.DueDate = dueDate;

                        if (!string.IsNullOrEmpty(ocrResult.Currency))
                            invoice.Currency = ocrResult.Currency;

                        invoice.AmountExclVat = ocrResult.AmountExclVat;
                        invoice.VatRate = ocrResult.VatRate;
                        invoice.VatAmount = ocrResult.VatAmount;
                        invoice.AmountInclVat = ocrResult.AmountInclVat;
                        invoice.PaidAmount = invoice.AmountInclVat;

                        if (!string.IsNullOrEmpty(ocrResult.SupplierVatCode))
                        {
                            var supplier = await context.BusinessPartners
                                .FirstOrDefaultAsync(s => s.VatCode == ocrResult.SupplierVatCode && s.IsActive, cancellationToken);
                            if (supplier != null)
                            {
                                invoice.SupplierId = supplier.Id;
                                invoice.PendingSupplierName = null;
                                invoice.PendingSupplierVat = null;
                                invoice.PendingSupplierAddress = null;
                            }
                            else
                            {
                                invoice.PendingSupplierName = ocrResult.SupplierName;
                                invoice.PendingSupplierVat = ocrResult.SupplierVatCode;
                                invoice.PendingSupplierAddress = null;
                            }
                        }
                        else if (!string.IsNullOrEmpty(ocrResult.SupplierName))
                        {
                            invoice.PendingSupplierName = ocrResult.SupplierName;
                            invoice.PendingSupplierVat = null;
                            invoice.PendingSupplierAddress = null;
                        }

                        context.ExpenseInvoices.Update(invoice);
                    }
                }

                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully processed queue item {QueueItemId}", queueItem.Id);
            }
            else
            {
                if (queueItem.Attempts >= queueItem.MaxAttempts)
                {
                    queueItem.Status = "FAILED";
                    queueItem.ErrorMessage = ocrResult.Diagnostics.AzureError ?? "Unknown error";
                    _logger.LogError("Queue item {QueueItemId} failed after {Attempts} attempts", queueItem.Id, queueItem.Attempts);
                }
                else
                {
                    queueItem.Status = "WAITING";
                    _logger.LogInformation("Queue item {QueueItemId} will be retried", queueItem.Id);
                }

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OCR Queue Worker stopping at {Time}", DateTimeOffset.Now);
            await base.StopAsync(stoppingToken);
        }
    }
}
