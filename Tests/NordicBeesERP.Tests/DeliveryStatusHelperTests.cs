using MudBlazor;
using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class DeliveryStatusHelperTests
{
    [Theory]
    [InlineData(0, 0, "RECEIVED")]
    [InlineData(100, 0, "PRICED")]
    [InlineData(100, 40, "PARTIAL_PAID")]
    [InlineData(100, 100, "PAID")]
    public void KeyFromAmounts_MapsDebtAndPayments(double debt, double paid, string expected) =>
        Assert.Equal(expected, DeliveryStatusHelper.KeyFromAmounts((decimal)debt, (decimal)paid));

    [Fact]
    public void Labels_AreSharedAcrossAllStatuses()
    {
        Assert.Equal("Priimtas", DeliveryStatusHelper.LabelForKey("RECEIVED"));
        Assert.Equal("Neapmokėtas", DeliveryStatusHelper.LabelForKey("PRICED"));
        Assert.Equal("Dalinai apmokėtas", DeliveryStatusHelper.LabelForKey("PARTIAL_PAID"));
        Assert.Equal("Apmokėtas", DeliveryStatusHelper.LabelForKey("PAID"));
        Assert.Equal(Color.Success, DeliveryStatusHelper.ColorForKey("PAID"));
        Assert.Equal("Priimtas", DeliveryStatusHelper.LabelForKey(null));
    }

    [Theory]
    [InlineData(0, 0, "Priimtas")]
    [InlineData(100, 0, "Neapmokėtas")]
    [InlineData(100, 40, "Dalinai apmokėtas")]
    [InlineData(100, 100, "Apmokėtas")]
    [InlineData(100, 150, "Apmokėtas")]
    public void Effective_IgnoresStoredStatus_AndUsesDebtAndPayments(int debt, int paid, string label)
    {
        // Stored status deliberately stale ("RECEIVED"): all windows must still agree on the amounts-derived state.
        var d = new NordicBeesERP.Models.WarehouseModule.Delivery { Status = "RECEIVED", TotalAmount = debt, PaidAmount = paid };
        Assert.Equal(label, DeliveryStatusHelper.GetLabel(d));
        Assert.Equal(DeliveryStatusHelper.KeyFromAmounts(debt, paid), DeliveryStatusHelper.KeyFor(d));
    }

    [Fact]
    public void Effective_NegativeDebt_IsReceived() =>
        Assert.Equal("RECEIVED", DeliveryStatusHelper.KeyFromAmounts(-5m, 0m));

    [Fact]
    public void Effective_PaidEqualsDebtWithFractions_IsPaid() =>
        Assert.Equal("PAID", DeliveryStatusHelper.KeyFromAmounts(10.05m, 10.05m));
}
