namespace NordicBeesERP.Helpers;

using NordicBeesERP.Models;
using System.Collections.Generic;

public static class SupplierFarmerHelper
{
    public static bool IsFarmer(Supplier supplier) =>
        supplier.IsIndividual
        && (supplier.DefaultVatRate == 6m || supplier.DefaultVatRate == 21m);

    public static List<string> GetMissingOrInvalidFields(Supplier supplier)
    {
        var result = new List<string>();
        if (!IsFarmer(supplier)) return result;

        if (string.IsNullOrWhiteSpace(supplier.SupplierFirstName))
            result.Add("Trūksta vardo (reikalinga ūkininkams)");
        if (string.IsNullOrWhiteSpace(supplier.SupplierLastName))
            result.Add("Trūksta pavardės (reikalinga ūkininkams)");

        if (supplier.DefaultVatRate == 6m)
        {
            if (string.IsNullOrWhiteSpace(supplier.CompensationVatCode))
                result.Add("Trūksta kompensacinio PVM tarifo kodo");
            else if (CompensationVatCodeValidator.IsObsoleteFormat(supplier.CompensationVatCode))
                result.Add("Kompensacinio PVM tarifo kodas yra pasenusio 13 skaitmenų formato — reikia perregistruoti");
            else if (!CompensationVatCodeValidator.IsValid(supplier.CompensationVatCode))
                result.Add("Neteisingas kompensacinio PVM tarifo kodas");
        }

        return result;
    }
}
