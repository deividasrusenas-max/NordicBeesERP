using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;

namespace NordicBeesERP.Tests.Playwright;

/// <summary>
/// Regression: the "Adresas" field in partner dialogs must accept typed text and save it
/// (it was blocked when it was a MudAutocomplete with CoerceValue/DebounceInterval).
///
/// Prerequisites: dev server on http://localhost:5081 and NORDICBEES_E2E_ADMIN_EMAIL /
/// NORDICBEES_E2E_ADMIN_PASSWORD set (never hardcoded). Creates one supplier named "E2E Adresas ..." in the dev DB.
/// </summary>
[Trait("Category", "E2E")]
public class AddressFieldE2ETests : IAsyncLifetime
{
    private const string Address = "Kalnų g. 5, Juodupė";
    private readonly string _baseUrl = "http://localhost:5081";
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public async Task InitializeAsync()
    {
        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task SupplierCreateDialog_AddressAcceptsTypedTextAndSaves()
    {
        var email = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD")
            ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set");
        var name = $"E2E Adresas {Guid.NewGuid():N}"[..24];

        await _page!.GotoAsync($"{_baseUrl}/login");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await _page.FillAsync("input[name=\"email\"]", email);
        await _page.FillAsync("input[name=\"password\"]", password);
        await _page.ClickAsync("form[action=\"/auth/login\"] button[type=submit]");
        await _page.WaitForURLAsync(u => !u.Contains("/login"), new() { Timeout = 15_000 });

        // SupplierCreateDialog is opened from the delivery form's supplier picker ("create new" row)
        await _page.GotoAsync($"{_baseUrl}/warehouse/deliveries/new");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var supplierInput = _page.GetByPlaceholder("Pradėkite rašyti tiekėjo pavadinimą...");
        await supplierInput.ClickAsync();
        await supplierInput.PressSequentiallyAsync(name, new() { Delay = 20 });
        await _page.ClickAsync("text=Sukurti naują tiekėją");

        await _page.GetByLabel("Adresas", new() { Exact = true }).WaitForAsync();
        await _page.GetByLabel("Pavadinimas", new() { Exact = true }).FillAsync(name);

        var address = _page.GetByLabel("Adresas", new() { Exact = true });
        await address.ClickAsync();
        await address.PressSequentiallyAsync(Address, new() { Delay = 30 });
        await _page.WaitForTimeoutAsync(800); // let any debounce/coerce logic run
        Assert.Equal(Address, await address.InputValueAsync());

        await _page.ClickAsync(".mud-dialog button:has-text(\"Išsaugoti\")");
        await _page.GetByLabel("Adresas", new() { Exact = true }).WaitForAsync(new() { State = WaitForSelectorState.Detached, Timeout = 10_000 });
    }
}
