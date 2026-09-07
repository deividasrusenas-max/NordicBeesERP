using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using Xunit;

namespace NordicBeesERP.Tests;

public class SupplierFarmerHelperTests
{
    private static Supplier Farmer(decimal rate, bool individual = true) => new()
    {
        IsIndividual = individual,
        DefaultVatRate = rate,
        SupplierFirstName = "Jonas",
        SupplierLastName = "Jonaitis",
        CompensationVatCode = "100008534429"
    };

    [Fact]
    public void IsFarmer_True_ForIndividualWith6PercentAnd21Percent()
    {
        Assert.True(SupplierFarmerHelper.IsFarmer(Farmer(6m)));
        Assert.True(SupplierFarmerHelper.IsFarmer(Farmer(21m)));
    }

    [Fact]
    public void IsFarmer_False_ForIndividualWithOtherRates_AndNonIndividual()
    {
        Assert.False(SupplierFarmerHelper.IsFarmer(Farmer(0m)));
        Assert.False(SupplierFarmerHelper.IsFarmer(Farmer(5m)));
        Assert.False(SupplierFarmerHelper.IsFarmer(Farmer(9m)));
        Assert.False(SupplierFarmerHelper.IsFarmer(Farmer(21m, individual: false)));
    }

    [Fact]
    public void CompleteFields_NoMissingOrInvalid()
    {
        var complete6 = Farmer(6m); // both names + valid code 100008534429
        Assert.Empty(SupplierFarmerHelper.GetMissingOrInvalidFields(complete6));

        var farmer21NoCode = Farmer(21m);
        farmer21NoCode.CompensationVatCode = null; // code not required at 21%
        Assert.Empty(SupplierFarmerHelper.GetMissingOrInvalidFields(farmer21NoCode));
    }

    [Fact]
    public void MissingNames_Reported_ForFarmers()
    {
        var noFirstName = Farmer(6m);
        noFirstName.SupplierFirstName = null;
        var missingFirst = SupplierFarmerHelper.GetMissingOrInvalidFields(noFirstName);
        Assert.Single(missingFirst);
        Assert.Contains("vardo", missingFirst[0]);

        var noLastName = Farmer(6m);
        noLastName.SupplierLastName = null;
        var missingLast = SupplierFarmerHelper.GetMissingOrInvalidFields(noLastName);
        Assert.Single(missingLast);
        Assert.Contains("pavardės", missingLast[0]);

        var noNames = Farmer(6m);
        noNames.SupplierFirstName = null;
        noNames.SupplierLastName = null;
        Assert.Equal(2, SupplierFarmerHelper.GetMissingOrInvalidFields(noNames).Count);
    }

    [Fact]
    public void CompensationCode_Missing_Reported_For6Percent()
    {
        var farmer = Farmer(6m);
        farmer.CompensationVatCode = null;
        var missing = SupplierFarmerHelper.GetMissingOrInvalidFields(farmer);
        Assert.Single(missing);
        Assert.Contains("kompensacinio", missing[0]);
    }

    [Fact]
    public void CompensationCode_Invalid_Reported_For6Percent()
    {
        var farmer = Farmer(6m);
        farmer.CompensationVatCode = "100008534428"; // bad check digit
        var missing = SupplierFarmerHelper.GetMissingOrInvalidFields(farmer);
        Assert.Single(missing);
        Assert.Contains("Neteisingas", missing[0]);
    }

    [Fact]
    public void CompensationCode_Obsolete13Digit_ReportedAsObsoleteFormat()
    {
        var farmer = Farmer(6m);
        farmer.CompensationVatCode = "1000085344296"; // 13 digits
        var missing = SupplierFarmerHelper.GetMissingOrInvalidFields(farmer);
        Assert.Single(missing);
        Assert.Contains("pasenusio", missing[0]);
    }

    [Fact]
    public void NonFarmer_AlwaysEmpty()
    {
        var nonIndividual = new Supplier
        {
            IsIndividual = false,
            DefaultVatRate = 6m,
            SupplierFirstName = null,
            SupplierLastName = null,
            CompensationVatCode = null
        };
        Assert.Empty(SupplierFarmerHelper.GetMissingOrInvalidFields(nonIndividual));
    }

    [Fact]
    public void CompensationCode_NotChecked_For21Percent()
    {
        var farmer = Farmer(21m);
        farmer.CompensationVatCode = "100008534428"; // invalid code, but irrelevant at 21%
        Assert.Empty(SupplierFarmerHelper.GetMissingOrInvalidFields(farmer));
    }
}
