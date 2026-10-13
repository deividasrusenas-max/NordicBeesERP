using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Integration tests against nordic_bees_erp_test. They require the manually applied
/// Migrations/Scripts/20261014_bucket_tare_ranges.sql and 20261014_container_type_enum_superset.sql
/// (agents never run DDL).
/// </summary>
[Collection("RealDatabase")]
public class BucketTareIntegrationTests : IClassFixture<DbTestFixture>
{
    private const string PreviousMigration = "20260927011841_AddSupplierAliases";
    private const string TareMigration = "20261014100000_AddBucketTareRanges";

    private readonly DbTestFixture _fixture;

    public BucketTareIntegrationTests(DbTestFixture fixture)
    {
        _fixture = fixture;
        BucketTareService.InvalidateCache();
    }

    private DeliveryService NewDeliveryService() => new(_fixture.Factory);
    private BucketTareService NewTareService() => new(_fixture.Factory);

    // ---- migration (script generation only: no DDL is executed) -------------------------------------------

    [Fact]
    public async Task Migration_UpScript_CreatesTableAndColumn_WithoutIfNotExists()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var migrator = context.GetService<IMigrator>();

        var up = migrator.GenerateScript(PreviousMigration, TareMigration);

        Assert.Contains("CREATE TABLE `bucket_tare_ranges`", up);
        Assert.Contains("`tare_manual`", up);
        Assert.Contains("INSERT INTO bucket_tare_ranges", up);
        Assert.DoesNotContain("IF NOT EXISTS", up, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migration_DownScript_DropsTableAndColumn()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();
        var migrator = context.GetService<IMigrator>();

        var down = migrator.GenerateScript(TareMigration, PreviousMigration);

        Assert.Contains("DROP TABLE `bucket_tare_ranges`", down);
        Assert.Contains("DROP COLUMN `tare_manual`", down);
    }

    [Fact]
    public async Task Migration_Applied_SeedRowsAndColumnPresent_HistoricContainersUntouched()
    {
        await using var context = await _fixture.Factory.CreateDbContextAsync();

        var seed = await context.BucketTareRanges.OrderBy(r => r.SortOrder).ToListAsync();
        Assert.Equal(
            new[] { (1.00m, 16.00m, 0.40m), (16.01m, 40.00m, 0.80m), (40.01m, 55.00m, 1.10m) },
            seed.Select(r => (r.MinBruttoKg, r.MaxBruttoKg, r.TareKg)).ToArray());
        Assert.All(seed, r => Assert.True(r.IsActive));

        // Rows that existed before the migration get tare_manual = 0 and are never recalculated.
        var historicManual = await context.Containers.CountAsync(c => c.TareManual && c.CreatedAt < new DateTime(2026, 10, 14));
        Assert.Equal(0, historicManual);
    }

    // ---- interval CRUD + cache invalidation ----------------------------------------------------------------

    [Fact]
    public async Task Crud_AddChangeDisable_CalculatorSeesNewValuesImmediately()
    {
        var service = NewTareService();
        var original = await service.GetRangesAsync(includeInactive: true);
        var addedId = 0;
        try
        {
            // add 55.01–60 → 1.5
            var withNew = original.Append(new BucketTareRange { MinBruttoKg = 55.01m, MaxBruttoKg = 60m, TareKg = 1.5m, IsActive = true }).ToList();
            Assert.Empty(await service.SaveRangesAsync(withNew));
            addedId = (await service.GetRangesAsync(true)).Single(r => r.MinBruttoKg == 55.01m).Id;

            var r58 = await service.GetTareAsync(58m);
            Assert.False(r58.IsOutOfRange);
            Assert.Equal(1.5m, r58.Tare);

            // change a tare
            var changed = (await service.GetRangesAsync(true)).ToList();
            changed.Single(r => r.MinBruttoKg == 16.01m).TareKg = 0.5m;
            Assert.Empty(await service.SaveRangesAsync(changed));
            Assert.Equal(0.5m, (await service.GetTareAsync(25m)).Tare);

            // soft-disable the new interval
            var disabled = (await service.GetRangesAsync(true)).ToList();
            disabled.Single(r => r.Id == addedId).IsActive = false;
            Assert.Empty(await service.SaveRangesAsync(disabled));
            Assert.True((await service.GetTareAsync(58m)).IsOutOfRange);
            Assert.Contains((await service.GetRangesAsync(true)), r => r.Id == addedId && !r.IsActive);
        }
        finally
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            if (addedId != 0)
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM bucket_tare_ranges WHERE id = {0}", addedId);
            await ctx.Database.ExecuteSqlRawAsync("UPDATE bucket_tare_ranges SET tare_kg = {0} WHERE min_brutto_kg = {1}", 0.80m, 16.01m);
            BucketTareService.InvalidateCache();
        }
    }

    [Fact]
    public async Task Save_OverlappingSet_ReturnsErrors_AndWritesNothing()
    {
        var service = NewTareService();
        var before = await service.GetRangesAsync(true);
        var bad = before.Select(r => new BucketTareRange
        {
            Id = r.Id, MinBruttoKg = r.MinBruttoKg, MaxBruttoKg = r.MaxBruttoKg, TareKg = r.TareKg, IsActive = r.IsActive
        }).ToList();
        bad[0].MaxBruttoKg = 20m; // overlaps 16.01–40

        var errors = await service.SaveRangesAsync(bad);

        Assert.NotEmpty(errors);
        BucketTareService.InvalidateCache();
        var after = await service.GetRangesAsync(true);
        Assert.Equal(before.Select(r => r.MaxBruttoKg), after.Select(r => r.MaxBruttoKg));
    }

    // ---- CreateDeliveryWithContainersAsync -----------------------------------------------------------------

    private static List<DeliveryLine> BucketLine(int count, decimal clientNet = 0m)
        => new() { new DeliveryLine { ContainerType = "BUCKET_GROUP", ContainerCount = count, TotalNetWeight = clientNet } };

    private async Task<List<Container>> LoadContainers(DeliveryTestData data)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        return await ctx.Containers.Where(c => c.ContainerCode.StartsWith(data.CodePrefix)).OrderBy(c => c.ContainerCode).ToListAsync();
    }

    [Fact]
    public async Task Create_ThreeBuckets_StoresAutoTareNettoAndValidContainerType()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var containers = new List<Container>
        {
            data.NewContainer("A", "BUCKET_GROUP", 5.0m, 0m),
            data.NewContainer("B", "BUCKET_GROUP", 25.0m, 0m),
            data.NewContainer("C", "BUCKET_GROUP", 50.0m, 0m)
        };

        var deliveryId = await NewDeliveryService().CreateDeliveryWithContainersAsync(data.NewDelivery(), BucketLine(3), containers);

        var stored = await LoadContainers(data);
        Assert.Equal(new[] { 0.4m, 0.8m, 1.1m }, stored.Select(c => c.TareWeight));
        Assert.Equal(new[] { 4.6m, 24.2m, 48.9m }, stored.Select(c => c.NetWeight));
        Assert.All(stored, c => Assert.False(c.TareManual));
        Assert.All(stored, c => Assert.Contains(c.ContainerType, new[] { "BUCKET", "BUCKET_GROUP" }));

        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var lineNet = await ctx.Database
            .SqlQueryRaw<decimal>("SELECT total_net_weight AS Value FROM delivery_lines WHERE delivery_id = {0}", deliveryId)
            .SingleAsync();
        Assert.Equal(77.7m, lineNet);
    }

    [Fact]
    public async Task Create_ManualTare_IsStoredWithFlag()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var bucket = data.NewContainer("A", "BUCKET_GROUP", 25.0m, 1.0m);
        bucket.TareManual = true;

        await NewDeliveryService().CreateDeliveryWithContainersAsync(data.NewDelivery(), BucketLine(1), new List<Container> { bucket });

        var stored = Assert.Single(await LoadContainers(data));
        Assert.Equal(1.0m, stored.TareWeight);
        Assert.True(stored.TareManual);
        Assert.Equal(24.0m, stored.NetWeight);
    }

    [Fact]
    public async Task Create_WrongClientNettoAndTare_AreRecalculatedByServer()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var bucket = data.NewContainer("A", "BUCKET_GROUP", 25.0m, 0.1m);
        bucket.NetWeight = 999m;

        await NewDeliveryService().CreateDeliveryWithContainersAsync(data.NewDelivery(), BucketLine(1, clientNet: 999m), new List<Container> { bucket });

        var stored = Assert.Single(await LoadContainers(data));
        Assert.Equal(0.8m, stored.TareWeight);
        Assert.Equal(24.2m, stored.NetWeight);
        Assert.False(stored.TareManual);
    }

    [Fact]
    public async Task Create_Brutto60_WithoutManualTare_IsRejected_AndNothingPersisted()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var bucket = data.NewContainer("A", "BUCKET_GROUP", 60.0m, 0m);

        await Assert.ThrowsAsync<BucketTareException>(() =>
            NewDeliveryService().CreateDeliveryWithContainersAsync(data.NewDelivery(), BucketLine(1), new List<Container> { bucket }));

        Assert.Empty(await LoadContainers(data));
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        Assert.Equal(0, await ctx.Deliveries.CountAsync(d => d.SupplierId == data.SupplierId));
    }

    [Fact]
    public async Task Create_Brutto60_WithManualTare_IsAccepted()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var bucket = data.NewContainer("A", "BUCKET_GROUP", 60.0m, 2.0m);
        bucket.TareManual = true;

        await NewDeliveryService().CreateDeliveryWithContainersAsync(data.NewDelivery(), BucketLine(1), new List<Container> { bucket });

        var stored = Assert.Single(await LoadContainers(data));
        Assert.Equal(2.0m, stored.TareWeight);
        Assert.Equal(58.0m, stored.NetWeight);
        Assert.True(stored.TareManual);
    }

    [Fact]
    public async Task Create_DoesNotTouchHistoricContainers()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var before = await Fingerprint(ctx, data.CodePrefix);

        await NewDeliveryService().CreateDeliveryWithContainersAsync(
            data.NewDelivery(), BucketLine(1), new List<Container> { data.NewContainer("A", "BUCKET_GROUP", 25.0m, 0m) });

        Assert.Equal(before, await Fingerprint(ctx, data.CodePrefix));
    }

    private static async Task<string> Fingerprint(NordicBeesERP.Data.NordicBeesERPContext ctx, string excludePrefix)
    {
        var rows = await ctx.Containers
            .Where(c => !c.ContainerCode.StartsWith(excludePrefix))
            .OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.GrossWeight, c.TareWeight, c.NetWeight, c.TareManual })
            .ToListAsync();
        return string.Join('|', rows.Select(r => $"{r.Id}:{r.GrossWeight}:{r.TareWeight}:{r.NetWeight}:{r.TareManual}"));
    }
}
