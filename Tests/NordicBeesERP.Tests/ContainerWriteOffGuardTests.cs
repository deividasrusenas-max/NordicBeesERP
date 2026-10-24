using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>#33: a container can be written off once; the reason is appended to notes, never replaces them.</summary>
[Collection("RealDatabase")]
[Trait("Category", "Db")]
public class ContainerWriteOffGuardTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ContainerWriteOffGuardTests(DbTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task WriteOff_Twice_ThrowsAndAddsNoSecondOutMovement_KeepsEarlierNotes()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var container = data.NewContainer("W", "BARREL", 120m, 20m);
        container.Notes = "pirminė pastaba";
        var lines = new List<DeliveryLine> { new() { ContainerType = "BARREL", ContainerCount = 1 } };
        await new DeliveryService(_fixture.Factory).CreateDeliveryWithContainersAsync(data.NewDelivery(), lines, new List<Container> { container });

        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var id = await ctx.Containers.AsNoTracking().Where(c => c.ContainerCode == container.ContainerCode).Select(c => c.Id).SingleAsync();
        var service = new ContainerService(_fixture.Factory);

        await service.WriteOffAsync(new List<int> { id }, "sugedo", null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.WriteOffAsync(new List<int> { id }, "dar kartą", null));

        var outCount = await ctx.Database.SqlQueryRaw<long>(
            "SELECT COUNT(*) AS Value FROM stock_movements WHERE container_id = {0} AND movement_type = 'OUT'", id).SingleAsync();
        Assert.Equal(1, outCount);
        var notes = await ctx.Database.SqlQueryRaw<string>("SELECT notes AS Value FROM containers WHERE id = {0}", id).SingleAsync();
        Assert.Equal("pirminė pastaba | sugedo", notes);
    }
}
