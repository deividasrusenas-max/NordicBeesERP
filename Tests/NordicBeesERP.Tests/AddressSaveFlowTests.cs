using System.Reflection;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NordicBeesERP.Components.Dialogs;
using NordicBeesERP.Components.Pages;
using NordicBeesERP.Data;
using NordicBeesERP.Models;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// For each of the four address forms: type "Adresas" by hand (or pick a Google suggestion), press "Išsaugoti",
/// and the (stubbed) save service must receive exactly that address. No database: EF InMemory + proxy stubs.
/// </summary>
public class AddressSaveFlowTests : IDisposable
{
    private const string Typed = "Kalnų g. 5, Juodupė";
    private readonly TestContext _ctx = new();
    private readonly PlacesStub _places = new() { Details = new PlaceAddressDetails("Kalnų g. 5", "Juodupė", "LT-41001", "Lietuva", "LT") };
    private Supplier? _savedSupplier;
    private Customer? _savedCustomer;
    private CompanySettings? _savedSettings;

    public AddressSaveFlowTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton<IGooglePlacesService>(_places);

        var dbName = Guid.NewGuid().ToString();
        _ctx.Services.AddDbContextFactory<NordicBeesERPContext>(o => o.UseInMemoryDatabase(dbName));
        _ctx.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<NordicBeesERPContext>>().CreateDbContext());

        _ctx.Services.AddSingleton(Stub.Create<ISupplierService>((name, args) =>
        {
            if (name != nameof(ISupplierService.SaveSupplierAsync)) return (false, null);
            _savedSupplier = (Supplier)args[0]!;
            return (true, Task.FromResult(_savedSupplier));
        }));
        _ctx.Services.AddSingleton(Stub.Create<ICustomerService>((name, args) =>
        {
            if (name != nameof(ICustomerService.SaveCustomerAsync)) return (false, null);
            _savedCustomer = (Customer)args[0]!;
            return (true, Task.FromResult(_savedCustomer));
        }));
        _ctx.Services.AddSingleton(Stub.Create<ICompanySettingsService>((name, args) =>
        {
            if (name != nameof(ICompanySettingsService.UpdateSettingsAsync)) return (false, null);
            _savedSettings = (CompanySettings)args[0]!;
            return (true, Task.CompletedTask);
        }));
        _ctx.Services.AddSingleton(Stub.Create<ICompanyLookupService>());
        _ctx.Services.AddSingleton(Stub.Create<IViesService>());
        _ctx.Services.AddSingleton(Stub.Create<IAuthService>());

        _ctx.RenderComponent<MudPopoverProvider>();
        _ctx.RenderComponent<MudSnackbarProvider>();
    }

    public void Dispose() => _ctx.Dispose();

    private async Task<IRenderedComponent<MudDialogProvider>> OpenDialogAsync<TDialog>(DialogParameters? parameters = null)
        where TDialog : ComponentBase
    {
        var provider = _ctx.RenderComponent<MudDialogProvider>();
        var dialogs = _ctx.Services.GetRequiredService<IDialogService>();
        await provider.InvokeAsync(() => dialogs.ShowAsync<TDialog>("test", parameters ?? new DialogParameters()));
        return provider;
    }

    private static void TypeAddress(IRenderedFragment host, string text)
        => host.FindComponents<MudTextField<string>>().First(t => t.Instance.Label == "Adresas").Find("input").Change(text);

    private static async Task PickSuggestionAsync(IRenderedFragment host)
    {
        var ac = host.FindComponent<MudAutocomplete<PlaceSuggestion>>();
        await host.InvokeAsync(() => ac.Instance.ValueChanged.InvokeAsync(new PlaceSuggestion("p1", Typed)));
    }

    private static async Task SaveAsync(IRenderedFragment host)
    {
        var button = host.FindAll("button").First(b => b.TextContent.Contains("Išsaugoti"));
        await host.InvokeAsync(() => button.Click());
    }

    // ---- SupplierCreateDialog ------------------------------------------------------------------

    [Fact]
    public async Task SupplierCreateDialog_ManualAddress_IsPassedToSave()
    {
        var host = await OpenDialogAsync<SupplierCreateDialog>();
        TypeAddress(host, Typed);
        await Task.Delay(1200);
        await SaveAsync(host);
        Assert.Equal(Typed, _savedSupplier?.Address);
    }

    [Fact]
    public async Task SupplierCreateDialog_GoogleSuggestion_FillsStreetCityPostalCode()
    {
        var host = await OpenDialogAsync<SupplierCreateDialog>();
        await PickSuggestionAsync(host);
        await SaveAsync(host);
        Assert.Equal("Kalnų g. 5", _savedSupplier?.Address);
        Assert.Equal("Juodupė", _savedSupplier?.City);
        Assert.Equal("LT-41001", _savedSupplier?.PostalCode);
    }

    // ---- SupplierEditDialog --------------------------------------------------------------------

    private static DialogParameters SupplierParams() => new() { { "Supplier", new Supplier { PartnerType = PartnerType.Supplier, IsSupplier = true } } };

    [Fact]
    public async Task SupplierEditDialog_ManualAddress_IsPassedToSave()
    {
        var host = await OpenDialogAsync<SupplierEditDialog>(SupplierParams());
        TypeAddress(host, Typed);
        await Task.Delay(1200);
        await SaveAsync(host);
        Assert.Equal(Typed, _savedSupplier?.Address);
    }

    [Fact]
    public async Task SupplierEditDialog_GoogleSuggestion_FillsStreetCityPostalCode()
    {
        var host = await OpenDialogAsync<SupplierEditDialog>(SupplierParams());
        await PickSuggestionAsync(host);
        await SaveAsync(host);
        Assert.Equal("Kalnų g. 5", _savedSupplier?.Address);
        Assert.Equal("Juodupė", _savedSupplier?.City);
        Assert.Equal("LT-41001", _savedSupplier?.PostalCode);
    }

    // ---- CustomerCreateDialog ------------------------------------------------------------------

    [Fact]
    public async Task CustomerCreateDialog_ManualAddress_IsPassedToSave()
    {
        var host = await OpenDialogAsync<CustomerCreateDialog>();
        TypeAddress(host, Typed);
        await Task.Delay(1200);
        await SaveAsync(host);
        Assert.Equal(Typed, _savedCustomer?.Address);
    }

    [Fact]
    public async Task CustomerCreateDialog_GoogleSuggestion_FillsStreetCityPostalCode()
    {
        var host = await OpenDialogAsync<CustomerCreateDialog>();
        await PickSuggestionAsync(host);
        await SaveAsync(host);
        Assert.Equal("Kalnų g. 5", _savedCustomer?.Address);
        Assert.Equal("Juodupė", _savedCustomer?.City);
        Assert.Equal("LT-41001", _savedCustomer?.PostalCode);
    }

    // ---- CompanySettingsPage -------------------------------------------------------------------

    [Fact]
    public async Task CompanySettingsPage_ManualAddress_IsPassedToUpdateSettings()
    {
        var page = _ctx.RenderComponent<CompanySettingsPage>();
        TypeAddress(page, Typed);
        await Task.Delay(1200);
        await SaveAsync(page);
        Assert.Equal(Typed, _savedSettings?.Address);
    }

    [Fact]
    public async Task CompanySettingsPage_GoogleSuggestion_FillsStreetCityPostalCode()
    {
        var page = _ctx.RenderComponent<CompanySettingsPage>();
        await PickSuggestionAsync(page);
        await SaveAsync(page);
        Assert.Equal("Kalnų g. 5", _savedSettings?.Address);
        Assert.Equal("Juodupė", _savedSettings?.City);
        Assert.Equal("LT-41001", _savedSettings?.PostalCode);
    }

    // ---- stubs ---------------------------------------------------------------------------------

    private sealed class PlacesStub : IGooglePlacesService
    {
        public PlaceAddressDetails? Details { get; set; }
        public Task<bool> IsHealthyAsync() => Task.FromResult(true);
        public Task<List<PlaceSuggestion>> AutocompleteAsync(string input, string sessionToken)
            => Task.FromResult(new List<PlaceSuggestion> { new("p1", Typed) });
        public Task<PlaceAddressDetails?> GetPlaceDetailsAsync(string placeId, string sessionToken) => Task.FromResult(Details);
    }

    /// <summary>Interface stub without a mocking library: unhandled calls return empty/default values.</summary>
    private class Stub : DispatchProxy
    {
        private Func<string, object?[], (bool Handled, object? Result)>? _handler;

        public static T Create<T>(Func<string, object?[], (bool, object?)>? handler = null) where T : class
        {
            var proxy = Create<T, Stub>();
            ((Stub)(object)proxy)._handler = handler;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (_handler?.Invoke(method!.Name, args ?? Array.Empty<object?>()) is { Handled: true } r) return r.Result;
            var ret = method!.ReturnType;
            if (ret == typeof(Task)) return Task.CompletedTask;
            if (ret == typeof(ValueTask)) return default(ValueTask);
            if (ret.IsGenericType && ret.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = ret.GetGenericArguments()[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, new[] { DefaultFor(inner) });
            }
            return DefaultFor(ret);
        }

        private static object? DefaultFor(Type t)
        {
            if (t == typeof(void)) return null;
            if (t == typeof(string)) return "";
            if (t.IsValueType) return Activator.CreateInstance(t);
            if (t.IsGenericType)
            {
                var def = t.GetGenericTypeDefinition();
                if (def == typeof(List<>)) return Activator.CreateInstance(t);
                if (def == typeof(IEnumerable<>) || def == typeof(IList<>) || def == typeof(IReadOnlyList<>) || def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                    return Activator.CreateInstance(typeof(List<>).MakeGenericType(t.GetGenericArguments()[0]));
            }
            if (t.IsClass && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null) return Activator.CreateInstance(t);
            return null;
        }
    }
}
