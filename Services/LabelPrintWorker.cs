using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;
using NordicBeesERP.Models.Printing;

namespace NordicBeesERP.Services;

/// <summary>
/// Background service that processes print_jobs queue.
/// - Polls every 1 second for PENDING jobs
/// - SemaphoreSlim(1) per printer_id to serialize prints per device
/// - Retry: max 3 with exponential backoff (1s, 2s, 4s)
/// - On final failure: status=FAILED + container_label_events INSERT (PRINT_FAILED)
/// </summary>
public class LabelPrintWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _printerLocks = new();

    public LabelPrintWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessPendingJobsAsync(stoppingToken);
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken ct)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NordicBeesERPContext>();
        var printerGateway = scope.ServiceProvider.GetRequiredService<IPrinterGateway>();

        var pendingJobs = await context.PrintJobs
            .Where(j => j.Status == "PENDING")
            .OrderBy(j => j.CreatedAt)
            .ToListAsync(ct);

        foreach (var job in pendingJobs)
        {
            if (ct.IsCancellationRequested) break;

            var lockObj = _printerLocks.GetOrAdd(job.PrinterId, _ => new SemaphoreSlim(1, 1));
            await lockObj.WaitAsync(ct);

            try
            {
                await ProcessJobAsync(context, printerGateway, job, ct);
            }
            catch (OperationCanceledException)
            {
                // Worker is stopping, skip remaining jobs
                break;
            }
            catch
            {
                // Job processing error — will be retried on next poll
            }
            finally
            {
                lockObj.Release();
            }
        }
    }

    private async Task ProcessJobAsync(NordicBeesERPContext context, IPrinterGateway gateway, PrintJob job, CancellationToken ct)
    {
        // Reload to check status hasn't changed
        var current = await context.PrintJobs.FindAsync(job.Id, ct);
        if (current == null || current.Status != "PENDING")
            return;

        // Mark as processing
        current.Status = "PROCESSING";
        current.ProcessedAt = DateTime.Now;
        await context.SaveChangesAsync(ct);

        var printer = await context.Printers.FindAsync(current.PrinterId, ct);
        if (printer == null)
        {
            current.Status = "FAILED";
            current.LastError = "Printer not found";
            await context.SaveChangesAsync(ct);
            await RecordPrintFailedEventAsync(context, current);
            return;
        }

        var result = await gateway.PrintAsync(current.ZplContent, printer);

        if (result.Success)
        {
            current.Status = "DONE";
            current.DoneAt = DateTime.Now;
            await context.SaveChangesAsync(ct);
        }
        else
        {
            current.RetryCount += 1;
            current.LastError = result.Message;

            if (current.RetryCount >= current.MaxRetries)
            {
                current.Status = "FAILED";
                await context.SaveChangesAsync(ct);
                await RecordPrintFailedEventAsync(context, current);
            }
            else
            {
                // Exponential backoff: keep status PROCESSING, will be retried after delay
                current.Status = "PENDING";
                await context.SaveChangesAsync(ct);

                var backoff = (int)Math.Pow(2, current.RetryCount - 1) * 1000; // 1s, 2s, 4s
                await Task.Delay(Math.Min(backoff, 10000), ct);
            }
        }
    }

    private static async Task RecordPrintFailedEventAsync(NordicBeesERPContext context, PrintJob job)
    {
        var @event = new ContainerLabelEvent
        {
            ContainerId = job.ContainerId,
            PrintJobId = job.Id,
            EventType = "PRINT_FAILED",
            OperatorId = job.CreatedByUserId,
            ReasonText = job.LastError ?? "Unknown error",
            CreatedAt = DateTime.Now
        };
        context.ContainerLabelEvents.Add(@event);
        await context.SaveChangesAsync();
    }
}
