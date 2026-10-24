using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>FIX-002: one Lithuanian label per payment method, one money format, one due-days wording.</summary>
public class Fix002HelperTests
{
    [Theory]
    [InlineData("bank_transfer", "Banko pavedimas")]
    [InlineData("BankTransfer", "Banko pavedimas")]
    [InlineData("BANK", "Banko pavedimas")]
    [InlineData("cash", "Grynais")]
    [InlineData("Cash", "Grynais")]
    [InlineData("CASH", "Grynais")]
    [InlineData("card", "Kortelė")]
    [InlineData("other", "Kita")]
    [InlineData("OTHER", "Kita")]
    [InlineData("offset", "Užskaita")]
    [InlineData("OFFSET", "Užskaita")]
    [InlineData("Offset", "Užskaita")]
    public void PaymentMethod_Label_IsLithuanianForBothVocabularies(string method, string expected) =>
        Assert.Equal(expected, PaymentMethodLabels.Label(method));

    [Fact]
    public void PaymentMethod_Offset_ShowsActNumber() =>
        Assert.Equal("Užskaita UZS-26-0001", PaymentMethodLabels.Label("offset", "UZS-26-0001"));

    [Fact]
    public void PaymentMethod_Unknown_IsShownAsIs() =>
        Assert.Equal("Something", PaymentMethodLabels.Label("Something"));

    [Theory]
    [InlineData(300, "300,00 €")]
    [InlineData(10000, "10 000,00 €")]
    [InlineData(11180.4, "11 180,40 €")]
    [InlineData(254.1, "254,10 €")]
    [InlineData(1.08, "1,08 €")]
    [InlineData(479510.57, "479 510,57 €")]
    [InlineData(-3855.36, "-3 855,36 €")]
    [InlineData(0, "0,00 €")]
    public void Money_IsTwoDecimalsWithSpaceThousands(double value, string expected) =>
        Assert.Equal(expected, FormatAmountHelper.Money((decimal)value));

    [Theory]
    [InlineData(-9, "vėluoja 9 d.")]
    [InlineData(0, "šiandien")]
    [InlineData(29, "liko 29 d.")]
    public void DueDays_Wording(int daysUntilDue, string expected) =>
        Assert.Equal(expected, DueDaysText.UntilDue(daysUntilDue));

    [Theory]
    [InlineData(-40, "40 d. anksčiau")]
    [InlineData(0, "Laiku")]
    [InlineData(5, "5 d. vėliau")]
    public void PaymentTiming_Wording(int daysFromDue, string expected) =>
        Assert.Equal(expected, DueDaysText.PaymentTiming(daysFromDue));
}
