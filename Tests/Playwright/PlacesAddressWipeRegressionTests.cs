using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace NordicBeesERP.Tests.Playwright;

/// <summary>
/// Regression tests for the Google-Places address-wipe bug.
///
/// Root cause: the partner dialogs' MudAutocomplete used @bind-Value on the
/// Address field plus OnValueChanged="OnPlaceSelectedAsync". Clicking a Google
/// Places suggestion made the binding write the clicked description into the
/// address field BEFORE the handler ran; the handler then did
/// `supplier.Address = details.StreetAddress;` unconditionally. Locality-level
/// places (no route/street_number address component) resolve to an empty
/// StreetAddress, and a failed details lookup returns null — in both cases the
/// previously stored real address was wiped/corrupted on save (dev rows
/// id=170/306 were wiped to '').
///
/// Fix (commits 8cacdc0, 6f62ce7, 6b14458): the handler now NEVER writes an
/// empty StreetAddress — on null details or empty street it keeps the clicked
/// description (which the binding already placed in the Address field) and
/// shows a warning snackbar; City/PostalCode/Country/CountryCode are only set
/// when the lookup returned a non-empty value.
///
/// These tests assert the observable regression property: after selecting a
/// locality-level suggestion, the Address field ALWAYS holds the clicked
/// description (never empty, never overwritten by an empty street). The tests
/// never click Save — they cancel the dialog — so no database row is modified.
///
/// Prerequisites:
///   - Dev server running on http://localhost:5081
///   - Admin credentials supplied via the NORDICBEES_E2E_ADMIN_EMAIL /
///     NORDICBEES_E2E_ADMIN_PASSWORD environment variables (never hardcoded —
///     see AGENTS.md "Secrets")
///   - Google Places API key configured and reachable (suggestions must load)
/// </summary>
[Trait("Category", "E2E")]
public class PlacesAddressWipeRegressionTests : IAsyncLifetime
{
    private const string _localityDescription = "Rokiškis, Rokiškis District Municipality, Lithuania";

    private readonly ITestOutputHelper _output;
    private IBrowser? _browser;
    private IPage? _page;
    private readonly string _baseUrl = "http://localhost:5081";
    private static readonly string _adminEmail =
        Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_EMAIL")
        ?? throw new Exception("NORDICBEES_E2E_ADMIN_EMAIL not set — see AGENTS.md Secrets rule, this test never hardcodes credentials");
    private static readonly string _adminPassword =
        Environment.GetEnvironmentVariable("NORDICBEES_E2E_ADMIN_PASSWORD")
        ?? throw new Exception("NORDICBEES_E2E_ADMIN_PASSWORD not set — see AGENTS.md Secrets rule");
    private readonly string _artifactsDir = ".playwright-mcp";

    public PlacesAddressWipeRegressionTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_artifactsDir);
    }

    public async Task InitializeAsync()
    {
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await playwright.Chromium.LaunchAsync(new() { Headless = Environment.GetEnvironmentVariable("E2E_HEADED") != "1" });
        _page = await _browser.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();
    }

    /// <summary>
    /// Logs in as the admin user if the app redirected to /login, then lands
    /// on /suppliers.
    /// </summary>
    private async Task EnsureLoggedInAsync()
    {
        await _page!.GotoAsync($"{_baseUrl}/suppliers");
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (_page.Url.Contains("/login"))
        {
            await _page.FillAsync("input[aria-label=\"El. paštas\"]", _adminEmail);
            await _page.FillAsync("input[aria-label=\"Slaptažodis\"]", _adminPassword);
            await _page.ClickAsync("text=Prisijungti");
            await _page.WaitForURLAsync(new Regex(".*"), new() { Timeout = 10_000 });
            await _page.GotoAsync($"{_baseUrl}/suppliers");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        await _page.WaitForSelectorAsync("text=Naujas tiekėjas");
    }

    /// <summary>
    /// Types a locality search into the Address autocomplete and clicks the
    /// "Rokiškis, Rokiškis District Municipality, Lithuania" suggestion.
    /// </summary>
    private async Task SelectLocalitySuggestionAsync()
    {
        await _page!.ClickAsync("input[aria-label=\"Adresas\"]");
        await _page.TypeAsync("input[aria-label=\"Adresas\"]", "Rokiškis", new() { Delay = 50 });

        var suggestion = _page.Locator("[role=\"option\"]", new() { HasText = _localityDescription });
        try
        {
            await suggestion.First.WaitForAsync(new() { Timeout = 20_000 });
        }
        catch (TimeoutException)
        {
            throw new Xunit.Sdk.XunitException(
                "Google Places suggestions did not load. This regression test requires the dev server " +
                "to reach the Google Places API (GOOGLE_PLACES_API_KEY configured and network reachable). " +
                "Address-wipe coverage cannot proceed without suggestions.");
        }

        await suggestion.First.ClickAsync();
        // Allow the OnPlaceSelectedAsync handler to finish after the popup closes.
        await _page.WaitForTimeoutAsync(500);
    }

    private async Task<string> ReadAddressValueAsync()
    {
        return await _page!.InputValueAsync("input[aria-label=\"Adresas\"]");
    }

    [Fact]
    public async Task SupplierEditDialog_SelectingLocality_KeepsAddressNonEmpty()
    {
        await EnsureLoggedInAsync();

        // Open the edit dialog of the FIRST supplier row. The test never saves,
        // so no database row is modified.
        var firstRowEditButton = _page!.Locator("tbody tr").First.Locator("button").First;
        await firstRowEditButton.ClickAsync();
        await _page.WaitForSelectorAsync("text=Atšaukti");

        var addressBefore = await ReadAddressValueAsync();
        _output.WriteLine($"Address before selection: '{addressBefore}'");

        await SelectLocalitySuggestionAsync();

        var addressAfter = await ReadAddressValueAsync();
        _output.WriteLine($"Address after locality selection: '{addressAfter}'");

        // THE regression assertion: the clicked description must be kept.
        // Pre-fix, locality-level places produced an empty StreetAddress that
        // overwrote this field (and later corrupted the database on save).
        Assert.Equal(_localityDescription, addressAfter);

        await TakeScreenshot("edit_dialog_locality_kept_address.png");

        // Cancel — never save.
        await _page.ClickAsync("text=Atšaukti");
    }

    [Fact]
    public async Task SupplierCreateDialog_SelectingLocality_KeepsDescription()
    {
        await EnsureLoggedInAsync();

        await _page!.ClickAsync("text=Naujas tiekėjas");
        await _page.WaitForSelectorAsync("text=Atšaukti");

        await SelectLocalitySuggestionAsync();

        var addressAfter = await ReadAddressValueAsync();
        _output.WriteLine($"Address after locality selection (create dialog): '{addressAfter}'");

        Assert.Equal(_localityDescription, addressAfter);

        await TakeScreenshot("create_dialog_locality_kept_description.png");

        // Cancel — never save.
        await _page.ClickAsync("text=Atšaukti");
    }

    private async Task TakeScreenshot(string filename)
    {
        await _page!.ScreenshotAsync(new()
        {
            Path = Path.Combine(_artifactsDir, filename)
        });
        _output.WriteLine($"Screenshot saved: {filename}");
    }
}
