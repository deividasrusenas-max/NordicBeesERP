using Microsoft.EntityFrameworkCore;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>The restored "Pradinis ID" format (pure, no DB): identical to GenerateSequentialId before commit ef1cefe.</summary>
public class ContainerCodeSequenceTests
{
    [Theory]
    [InlineData("100", 0, "100")]
    [InlineData("100", 1, "101")]
    [InlineData("100", 2, "102")]
    [InlineData("A-150", 2, "A-152")]
    [InlineData("A-009", 1, "A-010")]      // zero padding kept
    [InlineData("K-001", 11, "K-012")]
    [InlineData("TP000099", 1, "TP000100")]
    [InlineData("ABC", 0, "ABC")]          // no trailing number: first code is the start itself
    [InlineData("ABC", 2, "ABC-3")]        // ...then start-(offset+1)
    public void Generate_MatchesPreEf1cefeFormat(string start, int offset, string expected)
        => Assert.Equal(expected, ContainerCodeSequence.Generate(start, offset));

    [Fact]
    public void ThreeContainersFrom100_Are100_101_102()
        => Assert.Equal(new[] { "100", "101", "102" }, Enumerable.Range(0, 3).Select(i => ContainerCodeSequence.Generate("100", i)));

    [Fact]
    public void ThreeBucketsFromPrefixedStart_KeepPrefix()
        => Assert.Equal(new[] { "K-001", "K-002", "K-003" }, Enumerable.Range(0, 3).Select(i => ContainerCodeSequence.Generate("K-001", i)));

    [Theory]
    [InlineData("149", "150")]
    [InlineData("0", "1")]
    [InlineData("A-150", "")]   // only a purely numeric last code is auto-incremented (old behaviour)
    [InlineData(null, "")]
    public void SuggestStart_IsLastCodePlusOne_OnlyForNumericCodes(string? last, string expected)
        => Assert.Equal(expected, ContainerCodeSequence.SuggestStart(last));
}

[Collection("RealDatabase")]
public class ContainerNumberingIntegrationTests : IClassFixture<DbTestFixture>
{
    private readonly DbTestFixture _fixture;

    public ContainerNumberingIntegrationTests(DbTestFixture fixture) => _fixture = fixture;

    private static List<DeliveryLine> Line(string type, int count)
        => new() { new DeliveryLine { ContainerType = type, ContainerCount = count } };

    private async Task<List<Container>> Stored(DeliveryTestData data)
    {
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        return await ctx.Containers.Where(c => c.ContainerCode.StartsWith(data.CodePrefix)).OrderBy(c => c.Id).ToListAsync();
    }

    [Fact]
    public async Task StartId_ThreeBarrels_StoredAsStartPlusOne()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var start = $"{data.CodePrefix}-100";
        var containers = Enumerable.Range(0, 3)
            .Select(i => data.NewContainer("", "BARREL", 305m, 19m))
            .ToList();
        for (var i = 0; i < 3; i++) containers[i].ContainerCode = ContainerCodeSequence.Generate(start, i);

        await new DeliveryService(_fixture.Factory).CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BARREL", 3), containers);

        Assert.Equal(
            new[] { $"{data.CodePrefix}-100", $"{data.CodePrefix}-101", $"{data.CodePrefix}-102" },
            (await Stored(data)).Select(c => c.ContainerCode));
    }

    [Fact]
    public async Task StartId_ThreeBuckets_StoredAsStartPlusOne()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var start = $"{data.CodePrefix}-K001";
        var containers = Enumerable.Range(0, 3).Select(i => data.NewContainer("", "BUCKET_GROUP", 25m, 0m)).ToList();
        for (var i = 0; i < 3; i++) containers[i].ContainerCode = ContainerCodeSequence.Generate(start, i);

        await new DeliveryService(_fixture.Factory).CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BUCKET_GROUP", 3), containers);

        Assert.Equal(
            new[] { $"{data.CodePrefix}-K001", $"{data.CodePrefix}-K002", $"{data.CodePrefix}-K003" },
            (await Stored(data)).Select(c => c.ContainerCode));
    }

    [Fact]
    public async Task DefaultStart_IsLastBarrelCodePlusOne()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var numeric = (900_000_000 + Random.Shared.Next(1, 90_000_000)).ToString();
        var barrel = data.NewContainer("", "BARREL", 305m, 19m);
        barrel.ContainerCode = numeric;
        try
        {
            await new DeliveryService(_fixture.Factory).CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BARREL", 1), new List<Container> { barrel });

            var last = await new ContainerService(_fixture.Factory).GetLastContainerCodeAsync();

            Assert.Equal(numeric, last);
            Assert.Equal((long.Parse(numeric) + 1).ToString(), ContainerCodeSequence.SuggestStart(last));
        }
        finally
        {
            await using var ctx = await _fixture.Factory.CreateDbContextAsync();
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM stock_movements WHERE container_id IN (SELECT id FROM containers WHERE container_code = {0})", numeric);
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM containers WHERE container_code = {0}", numeric);
        }
    }

    [Fact]
    public async Task DuplicateOfExistingCode_ClearError_NothingWritten()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var service = new DeliveryService(_fixture.Factory);
        var existing = data.NewContainer("X", "BARREL", 305m, 19m);
        await service.CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BARREL", 1), new List<Container> { existing });

        var batch = new List<Container>
        {
            data.NewContainer("Y", "BARREL", 305m, 19m),
            data.NewContainer("X", "BARREL", 305m, 19m) // already exists
        };
        var ex = await Assert.ThrowsAsync<ContainerCodeConflictException>(() =>
            service.CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BARREL", 2), batch));

        Assert.Equal($"Kodas {data.Code("X")} jau egzistuoja", ex.Message);
        Assert.Equal(new[] { data.Code("X") }, (await Stored(data)).Select(c => c.ContainerCode)); // only the first delivery's container
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        Assert.Equal(1, await ctx.Deliveries.CountAsync(d => d.SupplierId == data.SupplierId));
    }

    [Fact]
    public async Task DuplicateInsideBatch_IsRejected_NothingWritten()
    {
        await using var data = await DeliveryTestData.CreateAsync(_fixture.Factory);
        var batch = new List<Container>
        {
            data.NewContainer("Z", "BARREL", 305m, 19m),
            data.NewContainer("Z", "BARREL", 305m, 19m)
        };

        await Assert.ThrowsAsync<ContainerCodeConflictException>(() =>
            new DeliveryService(_fixture.Factory).CreateDeliveryWithContainersAsync(data.NewDelivery(), Line("BARREL", 2), batch));

        Assert.Empty(await Stored(data));
    }

    [Fact]
    public async Task AutoBarrelNumberingSetting_DefaultsToFalse_AndFollowsAppSettings()
    {
        var containers = new ContainerService(_fixture.Factory);
        await using var ctx = await _fixture.Factory.CreateDbContextAsync();
        var previous = await ctx.AppSettings.Where(s => s.SettingKey == "AutoBarrelNumbering").Select(s => s.SettingValue).FirstOrDefaultAsync();
        try
        {
            await ctx.Database.ExecuteSqlRawAsync("DELETE FROM app_settings WHERE setting_key = {0}", "AutoBarrelNumbering");
            Assert.False(await containers.IsAutoNumberingEnabledAsync()); // missing row = false

            await Set(ctx, "true");
            Assert.True(await containers.IsAutoNumberingEnabledAsync());

            // flag on: the app uses GenerateNextContainerCodeAsync (TP + max + 1)
            var next = await containers.GenerateNextContainerCodeAsync();
            Assert.Matches(@"^TP\d{6,}$", next);

            await Set(ctx, "false");
            Assert.False(await containers.IsAutoNumberingEnabledAsync());
        }
        finally
        {
            if (previous is null)
                await ctx.Database.ExecuteSqlRawAsync("DELETE FROM app_settings WHERE setting_key = {0}", "AutoBarrelNumbering");
            else
                await Set(ctx, previous);
        }
    }

    private static Task<int> Set(NordicBeesERP.Data.NordicBeesERPContext ctx, string value)
        => ctx.Database.ExecuteSqlRawAsync(
            "INSERT INTO app_settings (setting_key, setting_value) VALUES ({0}, {1}) ON DUPLICATE KEY UPDATE setting_value = {1}",
            "AutoBarrelNumbering", value);
}
