using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

public class InvoiceTypesTests
{
    [Fact]
    public void IsReverseCharge96_ReverseChargeWith96StrType_ReturnsTrue()
    {
        Assert.True(InvoiceTypes.IsReverseCharge96(true, InvoiceTypes.ReverseCharge96));
    }

    [Fact]
    public void IsReverseCharge96_ReverseChargeWithUlak6PercentType_ReturnsFalse()
    {
        Assert.False(InvoiceTypes.IsReverseCharge96(true, InvoiceTypes.Ulak6));
    }

    [Fact]
    public void IsReverseCharge96_NoReverseChargeWithStandardType_ReturnsFalse()
    {
        Assert.False(InvoiceTypes.IsReverseCharge96(false, InvoiceTypes.Standard));
    }

    [Fact]
    public void IsReverseCharge96_ReverseChargeWithNullType_ReturnsTrue()
    {
        Assert.True(InvoiceTypes.IsReverseCharge96(true, null));
    }

    [Fact]
    public void IsReverseCharge96_NoReverseChargeWithNullType_ReturnsFalse()
    {
        Assert.False(InvoiceTypes.IsReverseCharge96(false, null));
    }
}
