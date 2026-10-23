using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using MySqlConnector;
using Xunit;
using Xunit.Abstractions;

namespace NordicBeesERP.Tests.Playwright;

/// <summary>
/// E2E for the honey-type feature (v0.17.114): set on create, set / change on DeliveryView, split + merge via
/// StockOverview, signed deliveries, blocked statuses, inactive types.
///
/// Prerequisites: dev server (dev DB, never prod) reachable at E2E_BASE_URL (default http://localhost:5081);
/// NORDICBEES_E2E_ADMIN_EMAIL / NORDICBEES_E2E_ADMIN_PASSWORD set (never hardcoded).
/// The DB connection is read at run time from E2E_DB_CONNECTION, else from the git-ignored appsettings.Local.json
/// (the same connection the locally started app uses) and is refused unless it is the dev database.
/// Every test seeds its own rows (code prefix "E2E-{tag}-") and removes them by id in finally.
/// After every UI action the DB is asserted, including the invariants a–f in <see cref="E2eDb.AssertInvariantsAsync"/>.
/// </summary>
[Trait("Category", "E2E")]
public class HoneyTypeE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly string _baseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL") ?? "http://localhost:5081";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public HoneyTypeE2ETests(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
        _page = await _browser.NewPageAsync();
        _page.SetDefaultTimeout(15_000);
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    // ───────────────────────── 1. ADD ─────────────────────────

    [Fact]
    public async Task Add_BucketDeliveryWithHoneyType_LineAndContainersCarryType()
    {
        var env = await E2eEnv.LoadAsync();
        var tag = E2eDb.NewTag();
        var sc = new Scenario(tag);
        try
        {
            await LoginAsync();
            await _page.GotoAsync($"{_baseUrl}/warehouse/deliveries/new");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var supplierInput = _page.GetByPlaceholder("Pradėkite rašyti tiekėjo pavadinimą...");
            await supplierInput.ClickAsync();
            await supplierInput.PressSequentiallyAsync(env.SupplierName, new() { Delay = 15 });
            await PopoverItem(env.SupplierName).First.ClickAsync();

            await PickSelectAsync(SelectByLabel("Sandėlis"), env.WarehouseName);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Toliau" }).ClickAsync();

            await _page.GetByRole(AriaRole.Button, new() { Name = "Medus", Exact = true }).ClickAsync();
            await _page.GetByRole(AriaRole.Button, new() { Name = "Kibirai", Exact = true }).ClickAsync();

            await PickSelectAsync(SelectByLabel("Medaus rūšis"), env.TypeA.Name);
            await _page.GetByLabel("Pradinis ID").FillAsync($"E2E-{tag}-001");

            var brutto = _page.GetByLabel("Brutto (kg)");
            await brutto.First.PressSequentiallyAsync("30", new() { Delay = 15 });
            await brutto.First.PressAsync("Enter");
            await Assertions.Expect(brutto).ToHaveCountAsync(2);
            await brutto.Nth(1).PressSequentiallyAsync("20", new() { Delay = 15 });
            await _page.Locator("[data-testid=bucket-add]").ClickAsync();
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Pridėta 2 vnt." })).ToBeVisibleAsync();

            await _page.GetByRole(AriaRole.Button, new() { Name = "Priimti pristatymą" }).ClickAsync();
            await _page.WaitForURLAsync(new Regex(@"/warehouse/deliveries/\d+"), new() { Timeout = 20_000 });
            sc.DeliveryId = int.Parse(Regex.Match(_page.Url, @"/deliveries/(\d+)").Groups[1].Value);

            var lines = await E2eDb.RowsAsync("SELECT id, honey_type_id FROM delivery_lines WHERE delivery_id=@d", ("d", sc.DeliveryId));
            Assert.Single(lines);
            Assert.Equal(env.TypeA.Id, Convert.ToInt32(lines[0]["honey_type_id"]));
            var containers = await E2eDb.RowsAsync("SELECT honey_type_id FROM containers WHERE container_code LIKE @p", ("p", sc.CodePrefix + "%"));
            Assert.Equal(2, containers.Count);
            Assert.All(containers, c => Assert.Equal(env.TypeA.Id, Convert.ToInt32(c["honey_type_id"])));

            await sc.CaptureMovementsAsync();
            await E2eDb.AssertInvariantsAsync(sc, _output, "create");
            Assert.All(sc.Movements.Values, q => Assert.True(q > 0)); // every container got its IN movement
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 2. SET when missing ─────────────────────────

    [Fact]
    public async Task Set_MissingType_OnDeliveryView_SetsLineAndAllContainers()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(null, null, new[] { 10m, 20m, 30m }));
        try
        {
            await LoginAsync();
            await OpenDeliveryAsync(sc);

            var select = LineSelect();
            await Assertions.Expect(select).ToBeVisibleAsync();
            await Assertions.Expect(select.Locator("input")).ToHaveAttributeAsync("placeholder", "Nenurodyta");
            await PickSelectAsync(select, env.TypeA.Name);

            await Assertions.Expect(_page.GetByText("Išsaugota", new() { Exact = true })).ToBeVisibleAsync();
            await AssertContainerTypeColumnAsync(env.TypeA.Name, 3);

            await WaitDbAsync(async () => await E2eDb.LineTypesAsync(sc) is { Count: 1 } t && t[0] == env.TypeA.Id);
            Assert.All(await E2eDb.ContainerTypesAsync(sc), t => Assert.Equal(env.TypeA.Id, t));
            await E2eDb.AssertInvariantsAsync(sc, _output, "set");
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 3. CHANGE ─────────────────────────

    [Fact]
    public async Task Change_Type_OnDeliveryView_StillOneLine_TotalsUnchanged()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(env.TypeA.Id, 2.5m, new[] { 10m, 20m, 30m }));
        try
        {
            var before = await E2eDb.DeliveryTotalsAsync(sc);
            await LoginAsync();
            await OpenDeliveryAsync(sc);

            await PickSelectAsync(LineSelect(), env.TypeB.Name);
            await Assertions.Expect(_page.GetByText("Išsaugota", new() { Exact = true })).ToBeVisibleAsync();
            await AssertContainerTypeColumnAsync(env.TypeB.Name, 3);

            await WaitDbAsync(async () => await E2eDb.LineTypesAsync(sc) is { Count: 1 } t && t[0] == env.TypeB.Id);
            Assert.All(await E2eDb.ContainerTypesAsync(sc), t => Assert.Equal(env.TypeB.Id, t));
            Assert.Equal(before, await E2eDb.DeliveryTotalsAsync(sc));
            await E2eDb.AssertInvariantsAsync(sc, _output, "change");
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 4. SPLIT + MERGE ─────────────────────────

    [Fact]
    public async Task SplitAndMerge_ViaStockOverview_KeepsPriceAndTotals_NoOrphanLine()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(env.TypeA.Id, 3.5m, new[] { 10m, 20m, 30m, 40m }));
        try
        {
            var before = await E2eDb.DeliveryTotalsAsync(sc);
            Assert.Equal("100.00|350.00", before);
            await LoginAsync();

            // split: two of the four buckets → type B
            await OpenStockAsync(sc);
            await SelectContainersAsync(sc.Codes[0], sc.Codes[1]);
            await AssignTypeAsync(env.TypeB.Name);
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Priskirta rūšis" })).ToBeVisibleAsync();

            await WaitDbAsync(async () => (await E2eDb.LineTypesAsync(sc)).Count == 2);
            var lines = await E2eDb.RowsAsync("SELECT honey_type_id, unit_price, total_net_weight, container_count FROM delivery_lines WHERE delivery_id=@d ORDER BY id", ("d", sc.DeliveryId));
            Assert.All(lines, l => Assert.Equal(3.5m, Convert.ToDecimal(l["unit_price"])));
            Assert.Equal(new[] { env.TypeA.Id, env.TypeB.Id }.OrderBy(i => i), lines.Select(l => Convert.ToInt32(l["honey_type_id"])).OrderBy(i => i));
            Assert.Equal(before, await E2eDb.DeliveryTotalsAsync(sc));
            await E2eDb.AssertInvariantsAsync(sc, _output, "split");

            // merge back: same two buckets → type A
            await OpenStockAsync(sc);
            await SelectContainersAsync(sc.Codes[0], sc.Codes[1]);
            await AssignTypeAsync(env.TypeA.Name);
            await Assertions.Expect(_page.Locator(".mud-snackbar", new() { HasText = "Priskirta rūšis" })).ToBeVisibleAsync();

            await WaitDbAsync(async () => (await E2eDb.LineTypesAsync(sc)).Count == 1);
            var merged = await E2eDb.RowsAsync("SELECT id, honey_type_id, unit_price, container_count FROM delivery_lines WHERE delivery_id=@d", ("d", sc.DeliveryId));
            var line = Assert.Single(merged);
            Assert.Equal(env.TypeA.Id, Convert.ToInt32(line["honey_type_id"]));
            Assert.Equal(4, Convert.ToInt32(line["container_count"]));
            var lineId = Convert.ToInt32(line["id"]);
            Assert.Equal(4L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM containers WHERE container_code LIKE @p AND delivery_line_id=@l", ("p", sc.CodePrefix + "%"), ("l", lineId)));
            Assert.Equal(before, await E2eDb.DeliveryTotalsAsync(sc));
            await E2eDb.AssertInvariantsAsync(sc, _output, "merge");
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 5. SIGNED ─────────────────────────

    [Fact]
    public async Task Signed_Delivery_TypeChangeAllowed_SignatureKept_ReceiptCleared_WeightsReadOnly()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(env.TypeA.Id, null, new[] { 10m, 20m }), signed: true, receipt: "e2e-cached-receipt.pdf");
        try
        {
            var sigBefore = await E2eDb.SignatureAsync(sc);
            Assert.StartsWith("e2e-cached-receipt.pdf", sigBefore.Split('|').Last());
            await LoginAsync();
            await OpenDeliveryAsync(sc);

            await Assertions.Expect(_page.GetByText("Aktas pasirašytas tiekėjo")).ToBeVisibleAsync();
            await PickSelectAsync(LineSelect(), env.TypeB.Name);
            await Assertions.Expect(_page.GetByText("Išsaugota", new() { Exact = true })).ToBeVisibleAsync();
            await AssertContainerTypeColumnAsync(env.TypeB.Name, 2);

            await WaitDbAsync(async () => await E2eDb.LineTypesAsync(sc) is { Count: 1 } t && t[0] == env.TypeB.Id);
            var sigAfter = await E2eDb.SignatureAsync(sc);
            Assert.Equal(sigBefore.Split('|')[0], sigAfter.Split('|')[0]); // signed_at, signer, svg unchanged
            Assert.Equal(sigBefore.Substring(0, sigBefore.LastIndexOf('|')), sigAfter.Substring(0, sigAfter.LastIndexOf('|')));
            Assert.Equal("", sigAfter.Split('|').Last()); // receipt_pdf_path NULL
            await Assertions.Expect(ContainersTable().Locator("input[inputmode=decimal]")).ToHaveCountAsync(0);
            await E2eDb.AssertInvariantsAsync(sc, _output, "signed");
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 6. BLOCKED ─────────────────────────

    [Fact]
    public async Task Blocked_ReservedContainer_NoLineSelect_StockOverviewRejects_DbUnchanged()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(env.TypeA.Id, 2m, new[] { 10m, 20m }, new[] { "IN_STOCK", "RESERVED" }));
        try
        {
            var snapshot = await E2eDb.SnapshotAsync(sc);
            await LoginAsync();

            await OpenDeliveryAsync(sc);
            await Assertions.Expect(LinesTable().Locator("tbody tr")).ToHaveCountAsync(1);
            await Assertions.Expect(LineSelect()).ToHaveCountAsync(0); // plain text, not editable
            await Assertions.Expect(LinesTable().Locator("tbody tr")).ToContainTextAsync(env.TypeA.Name);

            await OpenStockAsync(sc);
            await SelectContainersAsync(sc.Codes[1]); // the RESERVED one
            await AssignTypeAsync(env.TypeB.Name);
            var error = _page.Locator(".mud-snackbar", new() { HasText = "Nepavyko priskirti" });
            await Assertions.Expect(error).ToBeVisibleAsync();
            await Assertions.Expect(error).ToContainTextAsync("RESERVED");

            Assert.Equal(snapshot, await E2eDb.SnapshotAsync(sc));
            await E2eDb.AssertInvariantsAsync(sc, _output, "blocked");
        }
        finally { await E2eDb.CleanupAsync(sc); }
    }

    // ───────────────────────── 7. INACTIVE ─────────────────────────

    [Fact]
    public async Task Inactive_HoneyType_NotOfferedInEitherSelect()
    {
        var env = await E2eEnv.LoadAsync();
        var sc = await E2eDb.SeedAsync(env, new LineSpec(env.TypeA.Id, null, new[] { 10m, 20m }));
        var inactiveName = $"E2E Neaktyvi {sc.Tag}";
        long inactiveId = await E2eDb.ExecInsertAsync(
            "INSERT INTO honey_types (code, name, is_active) VALUES (@c, @n, 0)", ("c", $"E{sc.Tag}"), ("n", inactiveName));
        try
        {
            await LoginAsync();

            // DeliveryView line select
            await OpenDeliveryAsync(sc);
            await OpenSelectAsync(LineSelect());
            await Assertions.Expect(PopoverItem(env.TypeB.Name)).ToBeVisibleAsync(); // list is open and holds active types
            await Assertions.Expect(PopoverItem(inactiveName)).ToHaveCountAsync(0);
            await _page.Keyboard.PressAsync("Escape");

            // StockOverview assign dialog select
            await OpenStockAsync(sc);
            await SelectContainersAsync(sc.Codes[0]);
            await _page.GetByRole(AriaRole.Button, new() { Name = "Priskirti rūšį" }).ClickAsync();
            await OpenSelectAsync(SelectByLabel("Medaus rūšis"));
            await Assertions.Expect(PopoverItem(env.TypeB.Name)).ToBeVisibleAsync();
            await Assertions.Expect(PopoverItem(inactiveName)).ToHaveCountAsync(0);

            Assert.Equal(1L, await E2eDb.ScalarAsync<long>("SELECT COUNT(*) FROM honey_types WHERE id=@i AND is_active=0", ("i", inactiveId)));
            await E2eDb.AssertInvariantsAsync(sc, _output, "inactive");
        }
        finally
        {
            await E2eDb.ExecAsync("DELETE FROM honey_types WHERE id=@i", ("i", inactiveId));
            await E2eDb.CleanupAsync(sc);
        }
    }

    // ───────────────────────── UI helpers ─────────────────────────

    private async Task LoginAsync()
    {
        var email = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set");
        await _page.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.FillAsync("input[name=\"email\"]", email);
        await _page.FillAsync("input[name=\"password\"]", password);
        await _page.ClickAsync("form[action=\"/auth/login\"] button[type=submit]");
        await _page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });
    }

    private async Task OpenDeliveryAsync(Scenario sc)
    {
        await _page.GotoAsync($"{_baseUrl}/warehouse/deliveries/{sc.DeliveryId}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle); // Blazor circuit up: clicks on the prerendered page do nothing
        await Assertions.Expect(_page.GetByText($"Pristatymas {sc.DeliveryNumber}")).ToBeVisibleAsync();
    }

    private async Task OpenStockAsync(Scenario sc)
    {
        await _page.GotoAsync($"{_baseUrl}/warehouse/stock?warehouse={sc.WarehouseId}&q={Uri.EscapeDataString(sc.CodePrefix)}");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Assertions.Expect(_page.Locator("tr", new() { HasText = sc.Codes[0] })).ToBeVisibleAsync();
    }

    private async Task SelectContainersAsync(params string[] codes)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            var row = _page.Locator("tbody tr", new() { HasText = codes[i] });
            for (var attempt = 1; ; attempt++)
            {
                await row.Locator(".mud-checkbox").First.ClickAsync();
                try
                {
                    await _page.GetByText($"Pasirinkta: {i + 1} vnt.").WaitForAsync(new() { Timeout = 3_000 });
                    break;
                }
                catch (TimeoutException) when (attempt < 4) { }
            }
        }
    }

    private async Task AssignTypeAsync(string typeName)
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Priskirti rūšį" }).ClickAsync();
        await PickSelectAsync(SelectByLabel("Medaus rūšis"), typeName);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Priskirti ✓" }).ClickAsync();
    }

    private ILocator LinesTable() => _page.Locator(".mud-table").First;
    private ILocator ContainersTable() => _page.Locator(".mud-table").Last;
    private ILocator LineSelect() => LinesTable().Locator(".mud-input-control");
    private ILocator SelectByLabel(string label) => _page.Locator(".mud-input-control", new() { Has = _page.Locator($"label:text-is(\"{label}\")") });

    private ILocator PopoverItem(string text) =>
        _page.Locator(".mud-popover-open .mud-list-item").Filter(new() { HasTextRegex = new Regex($@"^\s*{Regex.Escape(text)}\s*$") });

    // Opens a MudSelect; a click that lands before the Blazor circuit is interactive opens nothing, so retry (bounded waits).
    private async Task OpenSelectAsync(ILocator select)
    {
        for (var attempt = 1; ; attempt++)
        {
            await select.ClickAsync();
            try
            {
                await _page.Locator(".mud-popover-open .mud-list-item").First.WaitForAsync(new() { Timeout = 3_000 });
                return;
            }
            catch (TimeoutException) when (attempt < 4) { }
        }
    }

    private async Task PickSelectAsync(ILocator select, string text)
    {
        await OpenSelectAsync(select);
        await PopoverItem(text).First.ClickAsync();
        await Assertions.Expect(_page.Locator(".mud-popover-open")).ToHaveCountAsync(0);
    }

    // Column "Rūšis" (3rd) of every container row shows the type name.
    private async Task AssertContainerTypeColumnAsync(string typeName, int rows)
    {
        var typeCells = ContainersTable().Locator("tbody tr td:nth-child(3)");
        await Assertions.Expect(ContainersTable().Locator("thead th", new() { HasText = "Rūšis" })).ToBeVisibleAsync();
        await Assertions.Expect(typeCells).ToHaveCountAsync(rows);
        await Assertions.Expect(typeCells).ToHaveTextAsync(Enumerable.Repeat(typeName, rows).ToArray());
    }

    // Poll the DB (the UI "Išsaugota" chip can show before a reload); no blind sleeps.
    private static async Task WaitDbAsync(Func<Task<bool>> condition, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(250);
        }
        Assert.True(await condition(), "DB condition not reached in time");
    }
}

// ───────────────────────── scenario + DB helpers ─────────────────────────

internal sealed record Named(int Id, string Name);

internal sealed record LineSpec(int? HoneyTypeId, decimal? UnitPrice, decimal[] Nets, string[]? Statuses = null);

internal sealed class E2eEnv
{
    public int WarehouseId; public string WarehouseName = ""; public int SupplierId; public string SupplierName = "";
    public int HoneyRawId; public Named TypeA = null!; public Named TypeB = null!;

    public static async Task<E2eEnv> LoadAsync()
    {
        var wh = (await E2eDb.RowsAsync("SELECT id, name FROM warehouses ORDER BY id LIMIT 1")).Single();
        var sup = (await E2eDb.RowsAsync("SELECT id, name FROM business_partners WHERE is_active=1 AND partner_type='supplier' AND name NOT LIKE '%\"%' ORDER BY id LIMIT 1")).Single();
        var raw = await E2eDb.ScalarAsync<int>("SELECT id FROM raw_material_types WHERE is_active=1 AND code='MD'");
        var types = await E2eDb.RowsAsync("SELECT id, name FROM honey_types WHERE is_active=1 ORDER BY id LIMIT 2");
        Assert.Equal(2, types.Count);
        return new E2eEnv
        {
            WarehouseId = Convert.ToInt32(wh["id"]), WarehouseName = (string)wh["name"]!,
            SupplierId = Convert.ToInt32(sup["id"]), SupplierName = (string)sup["name"]!,
            HoneyRawId = raw,
            TypeA = new(Convert.ToInt32(types[0]["id"]), (string)types[0]["name"]!),
            TypeB = new(Convert.ToInt32(types[1]["id"]), (string)types[1]["name"]!),
        };
    }
}

internal sealed class Scenario
{
    public Scenario(string tag) => Tag = tag;
    public string Tag { get; }
    public string CodePrefix => $"E2E-{Tag}-";
    public int DeliveryId;
    public int WarehouseId;
    public string DeliveryNumber => $"E2E-{Tag}";
    public List<string> Codes { get; } = new();
    public Dictionary<int, decimal> Movements { get; private set; } = new();

    public async Task CaptureMovementsAsync() => Movements = await E2eDb.InMovementsAsync(this);
}

internal static class E2eDb
{
    private static string? _connectionString;

    public static string NewTag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static string ConnectionString()
    {
        if (_connectionString != null) return _connectionString;
        var cs = Environment.GetEnvironmentVariable("E2E_DB_CONNECTION");
        if (string.IsNullOrWhiteSpace(cs))
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "appsettings.Local.json"))) dir = dir.Parent;
            if (dir == null) throw new Exception("appsettings.Local.json not found and E2E_DB_CONNECTION not set");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "appsettings.Local.json")));
            cs = doc.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
        }
        var b = new MySqlConnectionStringBuilder(cs!);
        // dev DB only: never the production host, never a database other than the dev one
        if (b.Server.Contains("10.255.8.5") || b.Database != "nordic_bees_erp")
            throw new Exception($"E2E refuses to run against {b.Server}/{b.Database} (dev database nordic_bees_erp only)");
        return _connectionString = cs!;
    }

    private static async Task<MySqlConnection> OpenAsync()
    {
        var c = new MySqlConnection(ConnectionString());
        await c.OpenAsync();
        return c;
    }

    private static MySqlCommand Cmd(MySqlConnection c, string sql, (string, object?)[] ps)
    {
        var cmd = new MySqlCommand(sql, c);
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    public static async Task<List<Dictionary<string, object?>>> RowsAsync(string sql, params (string, object?)[] ps)
    {
        await using var c = await OpenAsync();
        await using var r = await Cmd(c, sql, ps).ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public static async Task<T> ScalarAsync<T>(string sql, params (string, object?)[] ps)
    {
        await using var c = await OpenAsync();
        var v = await Cmd(c, sql, ps).ExecuteScalarAsync();
        return (T)Convert.ChangeType(v!, typeof(T), CultureInfo.InvariantCulture);
    }

    public static async Task ExecAsync(string sql, params (string, object?)[] ps)
    {
        await using var c = await OpenAsync();
        await Cmd(c, sql, ps).ExecuteNonQueryAsync();
    }

    public static async Task<long> ExecInsertAsync(string sql, params (string, object?)[] ps)
    {
        await using var c = await OpenAsync();
        var cmd = Cmd(c, sql, ps);
        await cmd.ExecuteNonQueryAsync();
        return cmd.LastInsertedId;
    }

    /// <summary>Consistent delivery with one line, one container per net weight, an IN movement per container.</summary>
    public static async Task<Scenario> SeedAsync(E2eEnv env, LineSpec spec, bool signed = false, string? receipt = null)
    {
        var sc = new Scenario(NewTag()) { WarehouseId = env.WarehouseId };
        var net = spec.Nets.Sum();
        var total = spec.UnitPrice.HasValue ? net * spec.UnitPrice.Value : 0m;
        try
        {
            sc.DeliveryId = (int)await ExecInsertAsync(
                @"INSERT INTO deliveries (delivery_number, delivery_date, supplier_id, warehouse_id, raw_material_type_id, status,
                    total_net_weight, total_amount, receipt_pdf_path, supplier_signed_at, supplier_signer_name, supplier_signature_svg)
                  VALUES (@n, CURDATE(), @s, @w, @r, @st, @nw, @ta, @rp,
                    CASE WHEN @sg = 1 THEN NOW() END, CASE WHEN @sg = 1 THEN 'E2E Signer' END, CASE WHEN @sg = 1 THEN '<svg/>' END)",
                ("n", sc.DeliveryNumber), ("s", env.SupplierId), ("w", env.WarehouseId), ("r", env.HoneyRawId),
                ("st", total > 0 ? "PRICED" : "RECEIVED"), ("nw", net), ("ta", total), ("rp", receipt), ("sg", signed ? 1 : 0));

            var lineId = await ExecInsertAsync(
                @"INSERT INTO delivery_lines (delivery_id, honey_type_id, container_type, container_count, total_net_weight, unit_price, line_total)
                  VALUES (@d, @h, 'BUCKET_GROUP', @cc, @nw, @up, @lt)",
                ("d", sc.DeliveryId), ("h", spec.HoneyTypeId), ("cc", spec.Nets.Length), ("nw", net), ("up", spec.UnitPrice),
                ("lt", spec.UnitPrice.HasValue ? total : null));

            for (var i = 0; i < spec.Nets.Length; i++)
            {
                var code = $"{sc.CodePrefix}{i + 1:D3}";
                sc.Codes.Add(code);
                var containerId = await ExecInsertAsync(
                    @"INSERT INTO containers (container_code, container_type, supplier_id, delivery_line_id, warehouse_id, honey_type_id,
                        gross_weight, tare_weight, net_weight, quantity, remaining_quantity, status)
                      VALUES (@c, 'BUCKET_GROUP', @s, @l, @w, @h, @g, 1, @n, 1, 1, @st)",
                    ("c", code), ("s", env.SupplierId), ("l", lineId), ("w", env.WarehouseId), ("h", spec.HoneyTypeId),
                    ("g", spec.Nets[i] + 1m), ("n", spec.Nets[i]), ("st", spec.Statuses?[i] ?? "IN_STOCK"));
                await ExecAsync(
                    "INSERT INTO stock_movements (container_id, movement_type, to_warehouse_id, quantity, reference_type) VALUES (@c, 'IN', @w, @q, 'Delivery')",
                    ("c", containerId), ("w", env.WarehouseId), ("q", spec.Nets[i]));
            }
            await sc.CaptureMovementsAsync();
            await AssertInvariantsAsync(sc, null, "seed");
            return sc;
        }
        catch
        {
            await CleanupAsync(sc);
            throw;
        }
    }

    public static async Task CleanupAsync(Scenario sc)
    {
        // a delivery created through the UI whose id was not captured: find it via this run's containers
        if (sc.DeliveryId == 0)
        {
            var found = await RowsAsync(
                "SELECT DISTINCT dl.delivery_id FROM delivery_lines dl JOIN containers c ON c.delivery_line_id = dl.id WHERE c.container_code LIKE @p",
                ("p", sc.CodePrefix + "%"));
            if (found.Count == 1) sc.DeliveryId = Convert.ToInt32(found[0]["delivery_id"]);
        }
        // by id only: the delivery's own lines / containers plus containers carrying this run's unique code prefix
        var ids = (await RowsAsync(
            @"SELECT c.id FROM containers c WHERE c.container_code LIKE @p
              UNION SELECT c.id FROM containers c JOIN delivery_lines dl ON dl.id = c.delivery_line_id WHERE dl.delivery_id = @d",
            ("p", sc.CodePrefix + "%"), ("d", sc.DeliveryId))).Select(r => Convert.ToInt32(r["id"])).ToList();
        foreach (var id in ids)
        {
            await ExecAsync("DELETE FROM stock_movements WHERE container_id = @i", ("i", id));
            await ExecAsync("DELETE FROM containers WHERE id = @i", ("i", id));
        }
        if (sc.DeliveryId > 0)
        {
            await ExecAsync("DELETE FROM delivery_lines WHERE delivery_id = @d", ("d", sc.DeliveryId));
            await ExecAsync("DELETE FROM deliveries WHERE id = @d", ("d", sc.DeliveryId));
        }
    }

    public static async Task<Dictionary<int, decimal>> InMovementsAsync(Scenario sc)
    {
        var rows = await RowsAsync(
            @"SELECT c.id, COALESCE(SUM(m.quantity), 0) AS q FROM containers c
              LEFT JOIN stock_movements m ON m.container_id = c.id AND m.movement_type = 'IN'
              WHERE c.container_code LIKE @p GROUP BY c.id", ("p", sc.CodePrefix + "%"));
        return rows.ToDictionary(r => Convert.ToInt32(r["id"]), r => Convert.ToDecimal(r["q"]));
    }

    public static async Task<List<int?>> LineTypesAsync(Scenario sc) =>
        (await RowsAsync("SELECT honey_type_id FROM delivery_lines WHERE delivery_id=@d ORDER BY id", ("d", sc.DeliveryId)))
            .Select(r => r["honey_type_id"] is null ? (int?)null : Convert.ToInt32(r["honey_type_id"])).ToList();

    public static async Task<List<int?>> ContainerTypesAsync(Scenario sc) =>
        (await RowsAsync("SELECT honey_type_id FROM containers WHERE container_code LIKE @p ORDER BY id", ("p", sc.CodePrefix + "%")))
            .Select(r => r["honey_type_id"] is null ? (int?)null : Convert.ToInt32(r["honey_type_id"])).ToList();

    public static async Task<string> DeliveryTotalsAsync(Scenario sc)
    {
        var r = (await RowsAsync("SELECT total_net_weight, total_amount FROM deliveries WHERE id=@d", ("d", sc.DeliveryId))).Single();
        return string.Create(CultureInfo.InvariantCulture, $"{Convert.ToDecimal(r["total_net_weight"]):0.00}|{Convert.ToDecimal(r["total_amount"]):0.00}");
    }

    /// <summary>signed_at|signer|svg|receipt_pdf_path (receipt last; NULL → "").</summary>
    public static async Task<string> SignatureAsync(Scenario sc)
    {
        var r = (await RowsAsync("SELECT supplier_signed_at, supplier_signer_name, supplier_signature_svg, receipt_pdf_path FROM deliveries WHERE id=@d", ("d", sc.DeliveryId))).Single();
        return string.Join("|", r["supplier_signed_at"], r["supplier_signer_name"], r["supplier_signature_svg"], r["receipt_pdf_path"]);
    }

    /// <summary>Everything a rejected change must leave alone (updated_at excluded).</summary>
    public static async Task<string> SnapshotAsync(Scenario sc)
    {
        var d = await RowsAsync("SELECT total_net_weight, total_amount, status, receipt_pdf_path FROM deliveries WHERE id=@d", ("d", sc.DeliveryId));
        var l = await RowsAsync("SELECT id, honey_type_id, unit_price, container_count, total_net_weight, line_total FROM delivery_lines WHERE delivery_id=@d ORDER BY id", ("d", sc.DeliveryId));
        var c = await RowsAsync("SELECT id, honey_type_id, delivery_line_id, status, net_weight, quantity FROM containers WHERE container_code LIKE @p ORDER BY id", ("p", sc.CodePrefix + "%"));
        string Dump(IEnumerable<Dictionary<string, object?>> rows) => string.Join(";", rows.Select(r => string.Join(",", r.Values.Select(v => v ?? "NULL"))));
        return $"{Dump(d)}#{Dump(l)}#{Dump(c)}";
    }

    /// <summary>a–f, asserted after every step; logs one line per call.</summary>
    public static async Task AssertInvariantsAsync(Scenario sc, ITestOutputHelper? output, string step)
    {
        var p = ("p", (object?)(sc.CodePrefix + "%"));
        var d = ("d", (object?)sc.DeliveryId);
        var problems = new List<string>();

        void Check(string name, List<Dictionary<string, object?>> bad)
        {
            if (bad.Count > 0) problems.Add($"{name}: {bad.Count} violation(s)");
        }

        Check("a line weight = SUM(container net)", await RowsAsync(
            @"SELECT dl.id FROM delivery_lines dl WHERE dl.delivery_id=@d AND EXISTS (SELECT 1 FROM containers c WHERE c.delivery_line_id=dl.id)
              AND ABS(COALESCE(dl.total_net_weight,0) - (SELECT SUM(c.net_weight) FROM containers c WHERE c.delivery_line_id=dl.id)) > 0.005", d));
        Check("b line_total = weight x unit_price", await RowsAsync(
            @"SELECT dl.id FROM delivery_lines dl WHERE dl.delivery_id=@d AND dl.unit_price IS NOT NULL
              AND ABS(COALESCE(dl.line_total,0) - COALESCE(dl.total_net_weight,0) * dl.unit_price) > 0.01", d));
        Check("c delivery totals = SUM(lines)", await RowsAsync(
            @"SELECT d.id FROM deliveries d WHERE d.id=@d AND (
                ABS(COALESCE(d.total_net_weight,0) - COALESCE((SELECT SUM(dl.total_net_weight) FROM delivery_lines dl WHERE dl.delivery_id=d.id),0)) > 0.005
                OR ABS(COALESCE(d.total_amount,0) - COALESCE((SELECT SUM(dl.line_total) FROM delivery_lines dl WHERE dl.delivery_id=d.id),0)) > 0.01)", d));
        Check("d1 container points to a deleted line", await RowsAsync(
            @"SELECT c.id FROM containers c WHERE c.container_code LIKE @p
              AND NOT EXISTS (SELECT 1 FROM delivery_lines dl WHERE dl.id=c.delivery_line_id)", p));
        Check("d2 container_count = SUM(quantity)", await RowsAsync(
            @"SELECT dl.id FROM delivery_lines dl WHERE dl.delivery_id=@d AND EXISTS (SELECT 1 FROM containers c WHERE c.delivery_line_id=dl.id)
              AND dl.container_count <> (SELECT SUM(c.quantity) FROM containers c WHERE c.delivery_line_id=dl.id)", d));
        Check("e container type = line type", await RowsAsync(
            @"SELECT c.id FROM containers c JOIN delivery_lines dl ON dl.id=c.delivery_line_id
              WHERE c.container_code LIKE @p AND NOT (c.honey_type_id <=> dl.honey_type_id)", p));

        var now = await InMovementsAsync(sc);
        if (sc.Movements.Count > 0 && !(now.Count == sc.Movements.Count && now.All(kv => sc.Movements.TryGetValue(kv.Key, out var q) && q == kv.Value)))
            problems.Add("f IN movement quantities changed");

        output?.WriteLine($"[invariants a-f] {step}: {(problems.Count == 0 ? "OK" : string.Join("; ", problems))}");
        Assert.True(problems.Count == 0, $"Invariants violated after '{step}': {string.Join("; ", problems)}");
    }
}
