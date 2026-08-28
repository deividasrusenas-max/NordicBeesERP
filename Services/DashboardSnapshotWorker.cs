using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services;

/// <summary>
/// Runs once daily at 03:00 AM and upserts a snapshot of key dashboard metrics
/// (barrels/buckets in stock, unpriced deliveries, supplier debt) into
/// dashboard_daily_snapshots.
/// </summary>
public class DashboardSnapshotWorker : BackgroundService
{
    private const int SnapshotHour = 3;

    private readonly ILogger<DashboardSnapshotWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<NordicBeesERPContext> _contextFactory;

    public DashboardSnapshotWorker(
        ILogger<DashboardSnapshotWorker> logger,
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<NordicBeesERPContext> contextFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _contextFactory = contextFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DashboardSnapshotWorker started. Next snapshot at 03:00 daily.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var nextRun = now.Date.AddHours(SnapshotHour);
            if (now >= nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation("DashboardSnapshotWorker: next snapshot at {NextRun:yyyy-MM-dd HH:mm:ss} (in {Delay}).", nextRun, delay.ToString(@"hh\:mm\:ss"));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            try
            {
                _logger.LogInformation("DashboardSnapshotWorker: computing snapshot for {Date}.", now.Date.ToString("yyyy-MM-dd"));
                await ComputeAndUpsertSnapshotAsync(stoppingToken);
                _logger.LogInformation("DashboardSnapshotWorker: snapshot upserted successfully.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DashboardSnapshotWorker: failed to compute/upsert daily snapshot. Will retry at next 03:00.");
            }
        }

        _logger.LogInformation("DashboardSnapshotWorker stopped.");
    }

    private async Task ComputeAndUpsertSnapshotAsync(CancellationToken cancellationToken)
    {
        // Resolve scoped services inside a scope (BackgroundService is singleton).
        using var scope = _scopeFactory.CreateScope();
        var containerService = scope.ServiceProvider.GetRequiredService<IContainerService>();
        var deliveryService = scope.ServiceProvider.GetRequiredService<IDeliveryService>();

        // 1. Barrels / buckets currently in stock.
        var inStockContainers = await containerService.GetFilteredAsync(null, null, null, "IN_STOCK", null);

        var barrels = inStockContainers.Where(c => c.ContainerType == "BARREL").ToList();
        var buckets = inStockContainers.Where(c => c.ContainerType != "BARREL").ToList();

        var barrelsCount = barrels.Count;
        var barrelsKg = barrels.Sum(c => c.NetWeight);
        var bucketsCount = buckets.Count;
        var bucketsKg = buckets.Sum(c => c.NetWeight);

        // 2. Unpriced deliveries (Status == "RECEIVED") and supplier debt.
        var deliveries = await deliveryService.GetAllAsync();

        var unpricedDeliveries = deliveries.Where(d => d.Status == "RECEIVED").ToList();
        var unpricedCount = unpricedDeliveries.Count;

        // Supplier debt: total amount owed minus what has been paid, only for positive balances.
        var debts = deliveries
            .Where(d => (d.TotalAmount - d.PaidAmount) > 0)
            .Select(d => d.TotalAmount - d.PaidAmount)
            .ToList();

        var supplierDebtTotal = debts.Sum();
        var supplierDebtCount = debts.Count;

        // 3. Upsert into dashboard_daily_snapshots keyed on snapshot_date (today).
        var snapshotDate = DateTime.Now.Date;

        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO dashboard_daily_snapshots 
            (snapshot_date, barrels_count, barrels_kg, buckets_count, buckets_kg, unpriced_deliveries_count, supplier_debt_total, supplier_debt_count, created_at)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, NOW())
            ON DUPLICATE KEY UPDATE
            barrels_count = {1}, barrels_kg = {2}, buckets_count = {3}, buckets_kg = {4}, 
            unpriced_deliveries_count = {5}, supplier_debt_total = {6}, supplier_debt_count = {7}, created_at = NOW()
            """,
            snapshotDate, barrelsCount, barrelsKg, bucketsCount, bucketsKg,
            unpricedCount, supplierDebtTotal, supplierDebtCount, cancellationToken);

        _logger.LogDebug(
            "DashboardSnapshotWorker: snapshot_date={Date} barrels={BarrelCount}/{BarrelKg:0.##}kg buckets={BucketCount}/{BucketKg:0.##}kg unpriced={Unpriced} debt={DebtTotal:0.##}x{DebtCount}",
            snapshotDate, barrelsCount, barrelsKg, bucketsCount, bucketsKg, unpricedCount, supplierDebtTotal, supplierDebtCount);
    }
}
