using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>A MudNumericField with the shared converter reads comma and dot input identically (no 100x).</summary>
public class LtNumberFieldTests : IDisposable
{
    private readonly TestContext _ctx = new();

    public LtNumberFieldTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();
        _ctx.RenderComponent<MudPopoverProvider>();
    }

    public void Dispose() => _ctx.Dispose();

    private decimal Type(string text, bool withConverter)
    {
        decimal value = 0m;
        var cut = _ctx.RenderComponent<MudNumericField<decimal>>(p =>
        {
            p.Add(c => c.Culture, LtCulture.Instance);
            if (withConverter) p.Add(c => c.Converter, LtNumberConverters.Decimal);
            p.Add(c => c.Value, 0m);
            p.Add(c => c.ValueChanged, EventCallback.Factory.Create<decimal>(this, v => value = v));
        });
        var input = cut.Find("input");
        input.Change(text);
        input.Blur();
        return value;
    }

    [Theory]
    [InlineData("11257,24", "11257.24")]
    [InlineData("11257.24", "11257.24")]
    [InlineData("308,80", "308.80")]
    [InlineData("1 125,50", "1125.50")]
    [InlineData("1.125,50", "1125.50")]
    [InlineData("19,5", "19.5")]
    public void SharedConverter_ParsesCommaAndDot(string text, string expected)
    {
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Type(text, withConverter: true));
    }
}
