using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Guards the UI container-type value ("BUCKET_GROUP") against the DB enum
/// (containers/delivery_lines.container_type). The dev/test DB enum is
/// ('BARREL','BUCKET'); InitialCreate declared ('BARREL','BUCKET_GROUP').
/// </summary>
[Collection("RealDatabase")]
public class DeliveryContainerTypeTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public DeliveryContainerTypeTests(DbTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateDeliveryWithContainersAsync_BucketDelivery_StoresValidContainerType()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var service = new DeliveryService(_fixture.Factory);

        var containers = new List<Container>
        {
            data.NewContainer("A", "BUCKET_GROUP", 29m, 0.8m),
            data.NewContainer("B", "BUCKET_GROUP", 15m, 0.4m)
        };
        var lines = new List<DeliveryLine>
        {
            new() { ContainerType = "BUCKET_GROUP", ContainerCount = 2, TotalNetWeight = 42.8m }
        };

        var deliveryId = await service.CreateDeliveryWithContainersAsync(data.NewDelivery(), lines, containers);
        data.DeliveryId = deliveryId;

        await using var verify = await _fixture.Factory.CreateDbContextAsync();
        var stored = await verify.Database
            .SqlQueryRaw<string>(
                "SELECT CAST(container_type AS CHAR) AS Value FROM containers WHERE container_code LIKE {0}",
                data.CodePrefix + "%")
            .ToListAsync();
        var storedLine = await verify.Database
            .SqlQueryRaw<string>(
                "SELECT CAST(container_type AS CHAR) AS Value FROM delivery_lines WHERE delivery_id = {0}", deliveryId)
            .ToListAsync();

        Assert.Equal(2, stored.Count);
        Assert.All(stored, t => Assert.Contains(t, new[] { "BARREL", "BUCKET" }));
        Assert.All(storedLine, t => Assert.Contains(t, new[] { "BARREL", "BUCKET" }));
    }
}
