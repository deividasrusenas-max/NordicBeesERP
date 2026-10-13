using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using NordicBeesERP.Components.Pages.Warehouse;
using NordicBeesERP.Models.WarehouseModule;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Renders the real DeliveryCreate page and checks the AutoBarrelNumbering switch: off = "Pradinis ID" fields and
/// start+1 codes, on = fields hidden and GenerateNextContainerCodeAsync codes. Services are default-returning stubs.
/// </summary>
public class DeliveryCreateNumberingTests : IDisposable
{
    private readonly TestContext _ctx = new();
    private readonly List<string> _generateNextCalls = new();
    private List<string> _existingCodes = new();

    public DeliveryCreateNumberingTests()
    {
        _ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        _ctx.Services.AddMudServices();

        _ctx.Services.AddSingleton(DefaultStub.For<IDeliveryService>());
        _ctx.Services.AddSingleton(DefaultStub.For<IHoneyTypeService>());
        _ctx.Services.AddSingleton(DefaultStub.For<IProductService>());
        _ctx.Services.AddSingleton(DefaultStub.For<IWarehouseService>());
        _ctx.Services.AddSingleton(DefaultStub.For<ICustomerService>());
        _ctx.Services.AddSingleton(DefaultStub.For<IRawMaterialTypeService>());
        _ctx.Services.AddSingleton<BucketTareCalculator>();
        _ctx.Services.AddSingleton(DefaultStub.For<IBucketTareService>(new()
        {
            ["GetRangesAsync"] = _ => (IReadOnlyList<BucketTareRange>)new List<BucketTareRange>
            {
                new() { Id = 1, MinBruttoKg = 1.00m, MaxBruttoKg = 16.00m, TareKg = 0.40m, SortOrder = 1, IsActive = true },
                new() { Id = 2, MinBruttoKg = 16.01m, MaxBruttoKg = 40.00m, TareKg = 0.80m, SortOrder = 2, IsActive = true },
                new() { Id = 3, MinBruttoKg = 40.01m, MaxBruttoKg = 55.00m, TareKg = 1.10m, SortOrder = 3, IsActive = true }
            }
        }));
    }

    public void Dispose() => _ctx.Dispose();

    private void RegisterContainerService(bool autoNumbering)
        => _ctx.Services.AddSingleton(DefaultStub.For<IContainerService>(new()
        {
            ["IsAutoNumberingEnabledAsync"] = _ => autoNumbering,
            ["GetLastContainerCodeAsync"] = _ => "149",
            ["GetLastBucketCodeAsync"] = _ => "89",
            ["GenerateNextContainerCodeAsync"] = _ => { _generateNextCalls.Add("call"); return "TP000007"; },
            ["FindExistingCodesAsync"] = _ => _existingCodes,
            ["GetByCodeAsync"] = _ => null
        }));

    private IRenderedComponent<DeliveryCreate> OpenStep3(string containerType, bool autoNumbering)
    {
        RegisterContainerService(autoNumbering);
        // Providers pull services, so they are rendered only after every service is registered.
        _ctx.RenderComponent<MudPopoverProvider>();
        _ctx.RenderComponent<MudSnackbarProvider>();
        var cut = _ctx.RenderComponent<DeliveryCreate>();
        Set(cut, "_step", 3);
        Set(cut, "_containerType", containerType);
        Set(cut, "_selectedRawMaterial", new RawMaterialType { Id = 1, Name = "MEDUS", IsHoney = true });
        cut.Render();
        return cut;
    }

    private static void Set(IRenderedComponent<DeliveryCreate> cut, string field, object value)
        => typeof(DeliveryCreate).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(cut.Instance, value);

    private static AngleSharp.Dom.IElement InputByLabel(IRenderedComponent<DeliveryCreate> cut, string label)
    {
        var forId = cut.FindAll("label").First(l => l.TextContent.Trim().StartsWith(label)).GetAttribute("for");
        return cut.Find("#" + forId);
    }

    private static IEnumerable<string> SummaryCodes(IRenderedComponent<DeliveryCreate> cut)
        => cut.FindAll("tbody tr td:first-child").Select(td => td.TextContent.Trim());

    private IEnumerable<string> Snackbars() => _ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message);

    [Fact]
    public void FlagOff_BarrelStep_ShowsStartIdPrefilledWithLastCodePlusOne()
    {
        var cut = OpenStep3("BARREL", autoNumbering: false);

        Assert.Equal("150", InputByLabel(cut, "Pradinis ID").GetAttribute("value"));
    }

    [Fact]
    public void FlagOn_BarrelStep_HidesStartId()
    {
        var cut = OpenStep3("BARREL", autoNumbering: true);

        Assert.DoesNotContain("Pradinis ID", cut.Markup);
    }

    [Fact]
    public void FlagOff_ThreeBarrelsFromStart100_Get100_101_102()
    {
        var cut = OpenStep3("BARREL", autoNumbering: false);
        InputByLabel(cut, "Pradinis ID").Change("100");
        InputByLabel(cut, "Kiekis").Change("3");

        cut.FindAll("button").First(b => b.TextContent.Contains("statinių")).Click();

        Assert.Equal(new[] { "100", "101", "102" }, SummaryCodes(cut));
        Assert.Empty(_generateNextCalls);
        // the next batch continues the sequence
        Assert.Equal("103", InputByLabel(cut, "Pradinis ID").GetAttribute("value"));
    }

    [Fact]
    public void FlagOn_BarrelsUseGenerateNextContainerCode()
    {
        var cut = OpenStep3("BARREL", autoNumbering: true);
        InputByLabel(cut, "Kiekis").Change("2");

        cut.FindAll("button").First(b => b.TextContent.Contains("statinių")).Click();

        Assert.Equal(new[] { "TP000007", "TP000008" }, SummaryCodes(cut));
        Assert.NotEmpty(_generateNextCalls);
    }

    [Fact]
    public void FlagOff_DuplicateCode_ShowsClearMessage_AndAddsNothing()
    {
        _existingCodes = new List<string> { "101" };
        var cut = OpenStep3("BARREL", autoNumbering: false);
        InputByLabel(cut, "Pradinis ID").Change("100");
        InputByLabel(cut, "Kiekis").Change("3");

        cut.FindAll("button").First(b => b.TextContent.Contains("statinių")).Click();

        Assert.Contains("Kodas 101 jau egzistuoja", Snackbars());
        Assert.Empty(SummaryCodes(cut));
    }

    [Fact]
    public void FlagOff_BucketStep_ShowsStartId_AndBucketsContinueFromIt()
    {
        var cut = OpenStep3("BUCKET_GROUP", autoNumbering: false);
        Assert.Equal("90", InputByLabel(cut, "Pradinis ID").GetAttribute("value"));

        var bruttoLabel = cut.FindAll("label").First(l => l.TextContent.StartsWith("Brutto"));
        cut.Find("#" + bruttoLabel.GetAttribute("for")).Input("25");
        cut.Find("[data-testid=bucket-add]").Click();

        Assert.Equal(new[] { "90" }, SummaryCodes(cut));
        Assert.Equal("91", InputByLabel(cut, "Pradinis ID").GetAttribute("value"));
    }

    [Fact]
    public void FlagOn_BucketStep_HidesStartId_AndUsesGeneratedCodes()
    {
        var cut = OpenStep3("BUCKET_GROUP", autoNumbering: true);
        Assert.DoesNotContain("Pradinis ID", cut.Markup);

        var bruttoLabel = cut.FindAll("label").First(l => l.TextContent.StartsWith("Brutto"));
        cut.Find("#" + bruttoLabel.GetAttribute("for")).Input("25");
        cut.Find("[data-testid=bucket-add]").Click();

        Assert.Equal(new[] { "TP000007" }, SummaryCodes(cut));
    }

    [Fact]
    public void FlagOff_BucketStartIdEmpty_BlocksAdd()
    {
        var cut = OpenStep3("BUCKET_GROUP", autoNumbering: false);
        InputByLabel(cut, "Pradinis ID").Change("");

        var bruttoLabel = cut.FindAll("label").First(l => l.TextContent.StartsWith("Brutto"));
        cut.Find("#" + bruttoLabel.GetAttribute("for")).Input("25");

        Assert.True(cut.Find("[data-testid=bucket-add]").HasAttribute("disabled"));
    }
}

/// <summary>DispatchProxy stub: Task methods complete with empty/default values unless overridden by method name.</summary>
internal static class DefaultStub
{
    public static T For<T>(Dictionary<string, Func<object?[], object?>>? overrides = null) where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Overrides = overrides ?? new();
        return proxy;
    }

    public class Proxy : DispatchProxy
    {
        public Dictionary<string, Func<object?[], object?>> Overrides { get; set; } = new();

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            var returnType = method!.ReturnType;
            object? value = Overrides.TryGetValue(method.Name, out var f) ? f(args ?? Array.Empty<object?>()) : DefaultFor(returnType);

            if (returnType == typeof(Task)) return Task.CompletedTask;
            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var inner = returnType.GetGenericArguments()[0];
                if (Overrides.ContainsKey(method.Name) is false) value = DefaultFor(inner);
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, new[] { value });
            }
            return value;
        }

        private static object? DefaultFor(Type type)
        {
            if (type == typeof(Task)) return null;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                type = type.GetGenericArguments()[0];

            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                var arg = type.GetGenericArguments()[0];
                if (def == typeof(List<>)) return Activator.CreateInstance(type);
                if (def == typeof(IEnumerable<>) || def == typeof(IList<>) || def == typeof(ICollection<>)
                    || def == typeof(IReadOnlyList<>) || def == typeof(IReadOnlyCollection<>))
                    return Activator.CreateInstance(typeof(List<>).MakeGenericType(arg));
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
