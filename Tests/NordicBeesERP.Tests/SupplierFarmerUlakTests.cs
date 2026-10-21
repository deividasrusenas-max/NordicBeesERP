using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using Xunit;

namespace NordicBeesERP.Tests;

public class SupplierFarmerUlakTests
{
    private static BusinessPartner Farmer(decimal rate = 6m) => new()
    {
        IsIndividual = true,
        DefaultVatRate = rate,
        SupplierFirstName = "Jonas",
        SupplierLastName = "Jonaitis",
        CompensationVatCode = "100008534429",
        NationalIdNumber = "10000000098",
        Address = "Testo g. 5, Vilnius",
        BankAccount = "LT121000011101001000"
    };

    [Fact]
    public void CompleteFarmer_NoProblems() => Assert.Empty(SupplierFarmerHelper.GetMissingOrInvalidFieldsForUlak(Farmer()));

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void ChangedDefaultRate_StillRequiresCompensationCode(int rate)
    {
        var p = Farmer(rate);
        p.CompensationVatCode = null;
        Assert.Contains(SupplierFarmerHelper.GetMissingOrInvalidFieldsForUlak(p), m => m.Contains("kompensacinio"));
    }

    [Fact]
    public void Company_IsRejected()
    {
        var p = Farmer();
        p.IsIndividual = false;
        Assert.Single(SupplierFarmerHelper.GetMissingOrInvalidFieldsForUlak(p));
    }
}
