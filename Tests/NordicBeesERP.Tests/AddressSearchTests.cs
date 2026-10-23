using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NordicBeesERP.Components.Shared;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>Address field = plain text field; Google lookup lives in AddressSearch and never touches the typed text.</summary>
public class AddressSearchTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly FakePlaces _places = new();

    public AddressSearchTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.Services.AddSingleton<IGooglePlacesService>(_places);
        _ctx.RenderComponent<MudPopoverProvider>();
    }

    public void Dispose() => _ctx.Dispose();

    private sealed class FakePlaces : IGooglePlacesService
    {
        public bool Healthy { get; set; } = true;
        public bool ThrowOnHealth { get; set; }
        public bool ThrowOnAutocomplete { get; set; }
        public PlaceAddressDetails? Details { get; set; }
        public Task<bool> IsHealthyAsync() => ThrowOnHealth ? throw new HttpRequestException("down") : Task.FromResult(Healthy);
        public Task<List<PlaceSuggestion>> AutocompleteAsync(string input, string sessionToken)
            => ThrowOnAutocomplete ? throw new HttpRequestException("down") : Task.FromResult(new List<PlaceSuggestion> { new("p1", "Kalnų g. 5, Juodupė") });
        public Task<PlaceAddressDetails?> GetPlaceDetailsAsync(string placeId, string sessionToken) => Task.FromResult(Details);
    }

    private sealed class Model { public string Address { get; set; } = ""; public string City { get; set; } = "Senas"; public string PostalCode { get; set; } = ""; public string Country { get; set; } = ""; public string CountryCode { get; set; } = ""; }

    private IRenderedComponent<AddressSearch> RenderSearch(Model model)
        => _ctx.RenderComponent<AddressSearch>(p => p.Add(x => x.OnPicked,
            EventCallback.Factory.Create<AddressPick>(this, pick =>
                pick.ApplyTo(v => model.Address = v, v => model.City = v, v => model.PostalCode = v, v => model.Country = v, v => model.CountryCode = v))));

    [Fact]
    public async Task AddressTextField_TypedValueStaysInModel_AfterDebounceWindow()
    {
        var model = new Model();
        var cut = _ctx.RenderComponent<MudTextField<string>>(p => p
            .Add(x => x.Label, "Adresas")
            .Add(x => x.Value, model.Address)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => model.Address = v)));

        cut.Find("input").Change("Kalnų g. 5, Juodupė");
        await Task.Delay(1200);

        Assert.Equal("Kalnų g. 5, Juodupė", model.Address);
    }

    [Fact]
    public async Task PickingSuggestion_FillsAllFields_AndClearsSearchBox()
    {
        _places.Details = new PlaceAddressDetails("Kalnų g. 5", "Juodupė", "LT-41001", "Lietuva", "LT");
        var model = new Model();
        var cut = RenderSearch(model);

        var ac = cut.FindComponent<MudAutocomplete<PlaceSuggestion>>();
        await cut.InvokeAsync(() => ac.Instance.ValueChanged.InvokeAsync(new PlaceSuggestion("p1", "Kalnų g. 5, Juodupė")));

        Assert.Equal("Kalnų g. 5", model.Address);
        Assert.Equal("Juodupė", model.City);
        Assert.Equal("LT-41001", model.PostalCode);
        Assert.Equal("Lietuva", model.Country);
        Assert.Equal("LT", model.CountryCode);
        Assert.Null(cut.FindComponent<MudAutocomplete<PlaceSuggestion>>().Instance.Value);
    }

    [Fact]
    public async Task PickingSuggestion_WithoutStreet_UsesDescription_AndKeepsExistingCityWhenUnknown()
    {
        _places.Details = new PlaceAddressDetails("", "", "", "Lietuva", "LT");
        var model = new Model();
        var cut = RenderSearch(model);

        var ac = cut.FindComponent<MudAutocomplete<PlaceSuggestion>>();
        await cut.InvokeAsync(() => ac.Instance.ValueChanged.InvokeAsync(new PlaceSuggestion("p1", "Juodupė, Lietuva")));

        Assert.Equal("Juodupė, Lietuva", model.Address);
        Assert.Equal("Senas", model.City);
        Assert.Equal("LT", model.CountryCode);
    }

    [Fact]
    public async Task PickingSuggestion_WhenDetailsFail_StillFillsAddressFromDescription()
    {
        _places.Details = null;
        var model = new Model();
        var cut = RenderSearch(model);

        var ac = cut.FindComponent<MudAutocomplete<PlaceSuggestion>>();
        await cut.InvokeAsync(() => ac.Instance.ValueChanged.InvokeAsync(new PlaceSuggestion("p1", "Kalnų g. 5, Juodupė")));

        Assert.Equal("Kalnų g. 5, Juodupė", model.Address);
    }

    [Theory]
    [InlineData(false, false)] // key empty / Google returns not healthy
    [InlineData(true, false)]  // health check throws
    public void GoogleDown_ShowsUnavailableMessage_AndNoSearchBox(bool throws, bool healthy)
    {
        _places.ThrowOnHealth = throws;
        _places.Healthy = healthy;

        var cut = RenderSearch(new Model());

        Assert.Contains("Google adresų paieška neprieinama", cut.Markup);
        Assert.Empty(cut.FindComponents<MudAutocomplete<PlaceSuggestion>>());
    }

    [Fact]
    public async Task GoogleDown_AddressFieldStaysEditable()
    {
        _places.ThrowOnHealth = true;
        var model = new Model();
        var search = RenderSearch(model);
        var field = _ctx.RenderComponent<MudTextField<string>>(p => p
            .Add(x => x.Value, model.Address)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => model.Address = v)));

        field.Find("input").Change("Kalnų g. 5, Juodupė");
        await Task.Delay(1200);

        Assert.Contains("neprieinama", search.Markup);
        Assert.Equal("Kalnų g. 5, Juodupė", model.Address);
    }

    [Fact]
    public async Task SearchThrowing_SwitchesToUnavailable_WithoutThrowing()
    {
        _places.ThrowOnAutocomplete = true;
        var cut = RenderSearch(new Model());
        var ac = cut.FindComponent<MudAutocomplete<PlaceSuggestion>>();

        var result = await cut.InvokeAsync(() => ac.Instance.SearchFunc("Kalnų", CancellationToken.None));

        Assert.Empty(result);
    }
}
