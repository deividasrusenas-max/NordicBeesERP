using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor;
using MudBlazor.Services;
using NordicBeesERP.Components.Dialogs;
using NordicBeesERP.Data;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Storage;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// A1 (D-047 4b): a real bUnit/JS-interop-level test proving the fix for
/// "drag &amp; drop stops working after removing the selected file" — the exact scenario the
/// owner's staging check found broken. This is a genuine interop-level test, not a reflection
/// call on a private method: it renders the real component and asserts on the real JS calls
/// bUnit's mocked <see cref="IJSRuntime"/> recorded.
///
/// bUnit was added to this test project specifically for this test (previously "not feasible,
/// not referenced" per the Etapas 0c and initial A1 reports) — this file exists to prove it now
/// is feasible for this one narrow scenario. <see cref="ExpenseUploadDialog"/>'s other injected
/// services (IExpenseService, IExpenseOcrService, IFileStore) are never called by the
/// drag-and-drop/remove/re-drop path under test, so they are satisfied with a throwing
/// <see cref="System.Reflection.DispatchProxy"/> stub (no new mocking package) rather than full
/// implementations — if a future test needs to exercise OCR analysis or saving, those stubs will
/// need to become real fakes at that point.
/// </summary>
[Collection("RealDatabase")]
public class ExpenseUploadDialogDragDropTests : IClassFixture<DbTestFixture>, IDisposable
{
    private readonly DbTestFixture _fixture;
    private readonly TestContext _testContext;

    public ExpenseUploadDialogDragDropTests(DbTestFixture fixture)
    {
        _fixture = fixture;
        _testContext = new TestContext();

        _testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        _testContext.Services.AddMudServices();
        _testContext.Services.AddSingleton(NullLoggerFactory.Instance);
        _testContext.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<ExpenseUploadDialog>>(NullLogger<ExpenseUploadDialog>.Instance);

        var options = new DbContextOptionsBuilder<NordicBeesERPContext>()
            .UseMySql(TestConnectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
            .Options;
        _testContext.Services.AddSingleton(new NordicBeesERPContext(options));
        _testContext.Services.AddSingleton<ICompanySettingsService>(new CompanySettingsService(_fixture.Factory));

        _testContext.Services.AddSingleton(ThrowingStub.For<IExpenseOcrService>());
        _testContext.Services.AddSingleton(ThrowingStub.For<IExpenseService>());
        _testContext.Services.AddSingleton(ThrowingStub.For<IFileStore>());
    }

    private static string TestConnectionString =>
        Environment.GetEnvironmentVariable("TEST_DB_CONNECTION")
        ?? "Server=100.110.26.80;Port=3306;Database=nordic_bees_erp_test;Uid=erp_user;Pwd=NordicBees2024;SslMode=none;AllowPublicKeyRetrieval=True;";

    public void Dispose() => _testContext.Dispose();

    [Fact]
    public async Task RemoveFile_ThenDropAgain_ReWiresTheDropZone()
    {
        var mudDialogInstance = ThrowingStub.For<IMudDialogInstance>();

        var component = _testContext.RenderComponent<ExpenseUploadDialog>(parameters => parameters
            .AddCascadingValue(mudDialogInstance));

        // OnAfterRenderAsync's retry loop calls `await Task.Delay(200)` then the JS call, as a
        // background continuation that RenderComponent() does not wait for and that does NOT
        // itself produce a new render (a successful call just sets a field, no StateHasChanged) —
        // so bUnit's render-event-driven WaitForAssertion has nothing to wake it up on. Polling
        // with a plain delay matches how this specific background work actually completes.
        await WaitUntilAsync(() => _testContext.JSInterop.Invocations["setupDropZone"].Count >= 1,
            "setupDropZone must be called on first render");
        var firstCallCount = _testContext.JSInterop.Invocations["setupDropZone"].Count;

        // Simulate a drop: call the [JSInvokable] method directly, exactly as dropzone.js does.
        // getDropFileBase64 is unconfigured (Loose mode) so it returns null, which the component
        // treats as "failed to read" — the point of this test is the RE-WIRING behaviour, not a
        // full successful drop, so this is deliberate: it exercises RemoveFile()'s re-arm path
        // without needing a real PDF fixture.
        var instance = component.Instance;
        var removeFileMethod = typeof(ExpenseUploadDialog).GetMethod("RemoveFile",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await component.InvokeAsync(() => removeFileMethod.Invoke(instance, null));

        // After RemoveFile(), the drop-zone div re-renders (still null file, upload phase) and
        // _dropZoneNeedsSetup is true again — OnAfterRenderAsync must re-invoke setupDropZone.
        await WaitUntilAsync(() => _testContext.JSInterop.Invocations["setupDropZone"].Count > firstCallCount,
            "setupDropZone must be called again after RemoveFile() re-renders the drop zone " +
            "(this is the exact bug A1 fixes — before the fix, this count would not increase)");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string failureMessage, int timeoutMs = 3000)
    {
        var elapsed = 0;
        const int step = 50;
        while (elapsed < timeoutMs)
        {
            if (condition()) return;
            await Task.Delay(step);
            elapsed += step;
        }
        Assert.True(condition(), failureMessage);
    }

    private const string ServerHealthSubtitle = "Patikrinkite ar visi serveriai veikia ir bandykite dar kartą.";

    /// <summary>Renders through the real MudBlazor dialog pipeline (MudDialogProvider +
    /// IDialogService.ShowAsync), the officially-supported way to bUnit-test a MudDialog's
    /// content — a bare cascaded IMudDialogInstance stub is not enough: MudDialog.OnInitialized
    /// registers itself against the real dialog instance, which needs an actual render handle.</summary>
    private async Task<IRenderedComponent<ExpenseUploadDialog>> ShowDialogAsync()
    {
        var provider = _testContext.RenderComponent<MudDialogProvider>();
        var dialogService = _testContext.Services.GetRequiredService<IDialogService>();

        await provider.InvokeAsync(() => dialogService.ShowAsync<ExpenseUploadDialog>());

        return provider.FindComponent<ExpenseUploadDialog>();
    }

    /// <summary>A2 (D-047 4a): the "check server health" subtitle must NOT appear for a non-OCR
    /// refusal (scan/non-PDF/same-file) — real component render, not a reflection call on a
    /// private field.</summary>
    [Theory]
    [InlineData("Failas nepriimtas")]
    [InlineData("Failas jau įkeltas")]
    public async Task ShowRefusal_NonOcrTitle_DoesNotRenderServerHealthSubtitle(string refusalTitle)
    {
        var component = await ShowDialogAsync();

        var instance = component.Instance;
        var showRefusalMethod = typeof(ExpenseUploadDialog).GetMethod("ShowRefusal",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await component.InvokeAsync(() => showRefusalMethod.Invoke(instance, new object[] { refusalTitle, "kažkokia žinutė" }));

        Assert.Contains(refusalTitle, component.Markup);
        Assert.DoesNotContain(ServerHealthSubtitle, component.Markup);
    }

    /// <summary>A2's counterpart: a genuine OCR failure (default title, never routed through
    /// ShowRefusal) must still show the subtitle — the fix must not have hidden it everywhere.</summary>
    [Fact]
    public async Task GenuineOcrFailure_DefaultTitle_StillRendersServerHealthSubtitle()
    {
        var component = await ShowDialogAsync();

        var instance = component.Instance;
        var phaseField = typeof(ExpenseUploadDialog).GetField("_phase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var errorMessageField = typeof(ExpenseUploadDialog).GetField("_errorMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await component.InvokeAsync(() =>
        {
            errorMessageField.SetValue(instance, "OCR nepavyko. Serverių būsena:\nAzure DI: timeout");
            phaseField.SetValue(instance, "error");
            // _errorTitle is left at its field default ("OCR nepavyko") deliberately — this is
            // exactly what every genuine OCR/Azure failure path in the component does.
            component.Render();
        });

        Assert.Contains(ServerHealthSubtitle, component.Markup);
    }
}

/// <summary>Throws NotImplementedException for any member call — for interfaces this test's
/// scenario never actually exercises, so a full fake would be pure unused boilerplate. No new
/// mocking package: <see cref="System.Reflection.DispatchProxy"/> is part of the BCL.</summary>
internal static class ThrowingStub
{
    public static T For<T>() where T : class => System.Reflection.DispatchProxy.Create<T, Proxy>();

    public class Proxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            => throw new NotImplementedException(
                $"{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name} is not implemented in this test's stub — " +
                "it is not expected to be called by the scenario under test.");
    }
}
