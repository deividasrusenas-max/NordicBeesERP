using NordicBeesERP.Helpers;
using NordicBeesERP.Services;
using Xunit;

namespace NordicBeesERP.Tests;

public class InvoiceSnapshotVatCodeTests
{
    [Fact]
    public void Ulak_FarmerWithoutVatCode_SnapshotsCompensationCode()
        => Assert.Equal("123456789012", InvoiceService.SnapshotVatCode(InvoiceTypes.Ulak6, null, "123456789012"));

    [Fact]
    public void Ulak_PrefersCompensationCodeOverLegacyVatCode()
        => Assert.Equal("123456789012", InvoiceService.SnapshotVatCode(InvoiceTypes.Ulak6, "LT000", "123456789012"));

    [Fact]
    public void Ulak_WithoutCompensationCode_FallsBackToVatCode()
        => Assert.Equal("LT000", InvoiceService.SnapshotVatCode(InvoiceTypes.Ulak6, "LT000", null));

    [Fact]
    public void StandardInvoice_KeepsVatCode()
        => Assert.Equal("LT000", InvoiceService.SnapshotVatCode(InvoiceTypes.Standard, "LT000", "123456789012"));
}
