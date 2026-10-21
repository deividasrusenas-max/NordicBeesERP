using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class InvoiceNumberingTests
{
    [Fact]
    public void EmptySeries_StartsAtOne() => Assert.Equal("ULAK26001", InvoiceService.NextInvoiceNumber("ULAK", "26", Array.Empty<string>()));

    [Fact]
    public void ContinuesAfterHighest() => Assert.Equal("LAK26043", InvoiceService.NextInvoiceNumber("LAK", "26", new[] { "LAK26001", "LAK26042", "LAK26017" }));

    [Fact]
    public void Past999_UsesNumericMaximumNotStringOrder()
        => Assert.Equal("ULAK261001", InvoiceService.NextInvoiceNumber("ULAK", "26", new[] { "ULAK26999", "ULAK261000" }));

    [Fact]
    public void IgnoresMalformedAndOtherSeries()
        => Assert.Equal("LAK26003", InvoiceService.NextInvoiceNumber("LAK", "26", new[] { "LAK26002", "LAK26abc", "LAK25009", "ULAK26050" }));
}
