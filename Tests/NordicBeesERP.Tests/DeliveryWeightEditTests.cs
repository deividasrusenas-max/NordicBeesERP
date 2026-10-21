using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// DeliveryService.UpdateContainerWeightAsync must cascade a container weight edit to the IN movement, the delivery
/// line, the delivery totals and the status (PR-MD2610-011: only containers changed, everything else stayed stale).
/// Integration tests against nordic_bees_erp_test.
/// </summary>
[Collection("RealDatabase")]
public class DeliveryWeightEditTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DeliveryWeightEditTests(DbTestFixture fixture) => _fixture = fixture;

    private DeliveryService NewService() => new(_fixture.Factory);

    private async Task<(int deliveryId, int lineId, List<Container> containers)> CreateBarrelDeliveryAsync(DeliveryTestData data)
    {
        var containers = new List<Container>
        {
            data.NewContainer("A", "BARREL", 320m, 20m),   // net 300
            data.NewContainer("B", "BARREL", 170m, 20m)    // net 150
        };
        var lines = new List<DeliveryLine> { new() { ContainerType = "BARREL", ContainerCount = 2 } };
        var deliveryId = await NewService().CreateDeliveryWithContainersAsync(data.NewDelivery(), lines, containers);

        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var lineId = await ctx.Database.SqlQueryRaw<int>("SELECT id AS Value FROM delivery_lines WHERE delivery_id = {0}", deliveryId).SingleAsync();
        var stored = await ctx.Containers.AsNoTracking().Where(c => c.ContainerCode.StartsWith(data.CodePrefix)).OrderBy(c => c.ContainerCode).ToListAsync();
        return (deliveryId, lineId, stored);
    }

    [Fact]
    public async Task WeightEdit_CascadesToMovementLineDeliveryAndPricedTotals()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, lineId, containers) = await CreateBarrelDeliveryAsync(data);
        await NewService().UpdatePricesAsync(deliveryId, new List<DeliveryLine> { new() { Id = lineId, UnitPrice = 2m } }, 0);

        await NewService().UpdateContainerWeightAsync(containers[0].Id, 350m, 20m, tareManual: false); // net 300 -> 330

        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        Assert.Equal(330m, (await ctx.Containers.AsNoTracking().FirstAsync(c => c.Id == containers[0].Id)).NetWeight);
        Assert.Equal(330m, await ctx.Database.SqlQueryRaw<decimal>(
            "SELECT quantity AS Value FROM stock_movements WHERE container_id = {0} AND movement_type = 'IN'", containers[0].Id).SingleAsync());
        var line = await ctx.DeliveryLines.AsNoTracking().FirstAsync(l => l.Id == lineId);
        Assert.Equal(480m, line.TotalNetWeight);
        Assert.Equal(960m, line.LineTotal);
        var delivery = await ctx.Deliveries.AsNoTracking().FirstAsync(d => d.Id == deliveryId);
        Assert.Equal(480m, delivery.TotalNetWeight);
        Assert.Equal(960m, delivery.TotalAmount);
        Assert.Equal("PRICED", delivery.Status);
    }

    [Fact]
    public async Task WeightEdit_TypoScaleValue_IsRejected_AndNothingChanges()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, _, containers) = await CreateBarrelDeliveryAsync(data);

        // "308,80" typed as 30880
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().UpdateContainerWeightAsync(containers[0].Id, 30900m, 20m, false));

        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        Assert.Equal(300m, (await ctx.Containers.AsNoTracking().FirstAsync(c => c.Id == containers[0].Id)).NetWeight);
        Assert.Equal(450m, (await ctx.Deliveries.AsNoTracking().FirstAsync(d => d.Id == deliveryId)).TotalNetWeight);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(100, 150)]
    [InlineData(100, -1)]
    public async Task WeightEdit_InvalidGrossTare_IsRejected(double gross, double tare)
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (_, _, containers) = await CreateBarrelDeliveryAsync(data);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().UpdateContainerWeightAsync(containers[0].Id, (decimal)gross, (decimal)tare, false));
    }

    [Fact]
    public async Task WeightEdit_NonInStockContainer_IsRejected()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (_, _, containers) = await CreateBarrelDeliveryAsync(data);
        await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            await ctx.Database.ExecuteSqlRawAsync("UPDATE containers SET status = 'WRITTEN_OFF' WHERE id = {0}", containers[0].Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().UpdateContainerWeightAsync(containers[0].Id, 350m, 20m, false));
    }

    [Fact]
    public async Task WeightEdit_SignedDelivery_IsRejected()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, _, containers) = await CreateBarrelDeliveryAsync(data);
        await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            await ctx.Database.ExecuteSqlRawAsync("UPDATE deliveries SET supplier_signed_at = NOW() WHERE id = {0}", deliveryId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NewService().UpdateContainerWeightAsync(containers[0].Id, 350m, 20m, false));
    }

    [Fact]
    public async Task WeightEdit_PartlyPaidDelivery_StaysPartialPaid()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, lineId, containers) = await CreateBarrelDeliveryAsync(data);
        await NewService().UpdatePricesAsync(deliveryId, new List<DeliveryLine> { new() { Id = lineId, UnitPrice = 2m } }, 0);
        await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO supplier_payments (delivery_id, supplier_id, amount, payment_date, payment_method, created_at) VALUES ({0}, {1}, 100, NOW(), 'bank_transfer', NOW())",
                deliveryId, data.SupplierId);

        await NewService().UpdateContainerWeightAsync(containers[0].Id, 350m, 20m, false);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        Assert.Equal("PARTIAL_PAID", (await verify.Deliveries.AsNoTracking().FirstAsync(d => d.Id == deliveryId)).Status);
        await verify.Database.ExecuteSqlRawAsync("DELETE FROM supplier_payments WHERE delivery_id = {0}", deliveryId);
    }

    [Fact]
    public async Task UpdatePrices_OnPartlyPaidDelivery_DoesNotResetStatusToReceived()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var (deliveryId, lineId, _) = await CreateBarrelDeliveryAsync(data);
        await NewService().UpdatePricesAsync(deliveryId, new List<DeliveryLine> { new() { Id = lineId, UnitPrice = 2m } }, 0);
        await using (var ctx = await _fixture.Factory.CreateDbContextAsync())
            await ctx.Database.ExecuteSqlRawAsync(
                "INSERT INTO supplier_payments (delivery_id, supplier_id, amount, payment_date, payment_method, created_at) VALUES ({0}, {1}, 100, NOW(), 'bank_transfer', NOW())",
                deliveryId, data.SupplierId);

        await NewService().UpdatePricesAsync(deliveryId, new List<DeliveryLine> { new() { Id = lineId, UnitPrice = 3m } }, 0);

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var d = await verify.Deliveries.AsNoTracking().FirstAsync(x => x.Id == deliveryId);
        Assert.Equal("PARTIAL_PAID", d.Status);
        Assert.Equal(1350m, d.TotalAmount);
        await verify.Database.ExecuteSqlRawAsync("DELETE FROM supplier_payments WHERE delivery_id = {0}", deliveryId);
    }
}
