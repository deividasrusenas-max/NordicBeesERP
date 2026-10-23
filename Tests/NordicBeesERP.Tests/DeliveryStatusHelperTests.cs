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
        Assert.Equal("Priimtas", DeliveryStatusHelper.GetLabel("RECEIVED"));
        Assert.Equal("Neapmokėtas", DeliveryStatusHelper.GetLabel("PRICED"));
        Assert.Equal("Dalinai apmokėtas", DeliveryStatusHelper.GetLabel("PARTIAL_PAID"));
        Assert.Equal("Apmokėtas", DeliveryStatusHelper.GetLabel("PAID"));
        Assert.Equal(Color.Success, DeliveryStatusHelper.GetColor("PAID"));
        Assert.Equal("Priimtas", DeliveryStatusHelper.GetLabel(null));
    }
}
