using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using NordicBeesERP.Components.Dialogs;
using NordicBeesERP.Models.Expenses;
using NordicBeesERP.Services;
using NordicBeesERP.Services.Dtos;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// D-048 Part D1: <see cref="ExpenseBudgetDialog"/>'s <c>Year</c> parameter used to be dead —
/// assigned by the caller (<c>ExpensesDashboard.OpenBudgetDialogAsync</c>, which passes
/// <c>DateTime.Today.Year</c>) but never read; the dialog always queried its own
/// <c>_year</c> field (defaulting to <c>DateTime.Now.Year</c>) instead. Both happened to equal
/// "this year" today, which is why the bug was invisible in normal use. Fixed by seeding
/// <c>_year</c> from <c>Year</c> in <c>OnInitialized</c> (not <c>OnParametersSetAsync</c>, so a
/// later edit of the "Metai" textbox by the user isn't stomped on by a re-render). This test
/// proves the wiring with a YEAR THE CALLER PASSES THAT IS NOT THE CURRENT YEAR, so the fix
/// can't be masked by the coincidence that broke visibility of the original bug.
/// </summary>
public class ExpenseBudgetDialogYearTests : IDisposable
{
    private readonly TestContext _testContext;

    public ExpenseBudgetDialogYearTests()
    {
        _testContext = new TestContext();
        _testContext.JSInterop.Mode = JSRuntimeMode.Loose;
        _testContext.Services.AddMudServices();
        _testContext.Services.AddSingleton(NullLoggerFactory.Instance);
    }

    public void Dispose() => _testContext.Dispose();

    [Fact]
    public void Year_Parameter_DrivesTheInitialQueries_NotJustTodaysYear()
    {
        const int callerYear = 2019; // deliberately not DateTime.Now.Year
        var requestedYears = new List<int>();

        var capturingService = ConfigurableStub.For<IExpenseService>(new()
        {
            [nameof(IExpenseService.GetCategoriesAsync)] = _ => Task.FromResult(new List<ExpenseCategory>()),
            [nameof(IExpenseService.GetBudgetsAsync)] = args =>
            {
                requestedYears.Add((int)args![1]!);
                return Task.FromResult(new List<ExpenseBudget>());
            },
            [nameof(IExpenseService.GetBudgetActualsAsync)] = args =>
            {
                requestedYears.Add((int)args![0]!);
                return Task.FromResult(new BudgetActualsResult());
            }
        });
        _testContext.Services.AddSingleton(capturingService);

        _testContext.RenderComponent<ExpenseBudgetDialog>(parameters => parameters
            .Add(p => p.Year, callerYear));

        Assert.NotEmpty(requestedYears);
        Assert.All(requestedYears, y => Assert.Equal(callerYear, y));
    }
}

/// <summary>Selective-override variant of the <c>ThrowingStub</c> pattern used elsewhere in this
/// test project (see ExpenseUploadDialogDragDropTests): still a <see cref="System.Reflection.DispatchProxy"/>
/// stub (no new mocking package), but lets a test override just the handful of methods its
/// scenario actually calls, by name, while every other member still throws — needed here because
/// IExpenseService is large and this test only cares about three of its ~55 members.</summary>
internal static class ConfigurableStub
{
    public static T For<T>(Dictionary<string, Func<object?[]?, object?>> overrides) where T : class
    {
        var proxy = System.Reflection.DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Overrides = overrides;
        return proxy;
    }

    public class Proxy : System.Reflection.DispatchProxy
    {
        public Dictionary<string, Func<object?[]?, object?>> Overrides = new();

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod != null && Overrides.TryGetValue(targetMethod.Name, out var fn))
                return fn(args);

            throw new NotImplementedException(
                $"{targetMethod?.DeclaringType?.Name}.{targetMethod?.Name} is not stubbed in this test — " +
                "it is not expected to be called by the scenario under test.");
        }
    }
}
