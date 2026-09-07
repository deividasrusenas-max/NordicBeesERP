namespace NordicBeesERP.Helpers;

using NordicBeesERP.Models;
using System.Collections.Generic;

public static class SupplierFarmerHelper
{
    // --- public API ---

    public static bool IsFarmer(Supplier supplier) =>
        IsFarmer(supplier.IsIndividual, supplier.DefaultVatRate);

    public static bool IsFarmer(BusinessPartner partner) =>
        IsFarmer(partner.IsIndividual, partner.DefaultVatRate);

    public static List<string> GetMissingOrInvalidFields(Supplier supplier) =>
        CheckMissingOrInvalidFields(
            supplier.IsIndividual, supplier.DefaultVatRate,
            supplier.SupplierFirstName, supplier.SupplierLastName,
            supplier.CompensationVatCode,
            supplier.NationalIdNumber, supplier.Address, supplier.BankAccount);

    public static List<string> GetMissingOrInvalidFields(BusinessPartner partner) =>
        CheckMissingOrInvalidFields(
            partner.IsIndividual, partner.DefaultVatRate,
            partner.SupplierFirstName, partner.SupplierLastName,
            partner.CompensationVatCode,
            partner.NationalIdNumber, partner.Address, partner.BankAccount);

    // --- private shared cores (single source of truth) ---

    private static bool IsFarmer(bool isIndividual, decimal defaultVatRate) =>
        isIndividual && (defaultVatRate == 6m || defaultVatRate == 21m);

    private static List<string> CheckMissingOrInvalidFields(
        bool isIndividual, decimal defaultVatRate,
        string? supplierFirstName, string? supplierLastName, string? compensationVatCode,
        string? nationalIdNumber, string? address, string? bankAccount)
    {
        var result = new List<string>();
        if (!IsFarmer(isIndividual, defaultVatRate)) return result;

        if (string.IsNullOrWhiteSpace(supplierFirstName))
            result.Add("Trūksta vardo (reikalinga ūkininkams)");
        if (string.IsNullOrWhiteSpace(supplierLastName))
            result.Add("Trūksta pavardės (reikalinga ūkininkams)");

        if (string.IsNullOrWhiteSpace(nationalIdNumber))
            result.Add("Asmens kodas nenurodytas");
        else if (!LithuanianIdValidator.IsValid(nationalIdNumber))
            result.Add("Neteisingas asmens kodas");

        if (string.IsNullOrWhiteSpace(address))
            result.Add("Adresas nenurodytas");

        if (string.IsNullOrWhiteSpace(bankAccount))
            result.Add("Banko sąskaitos numeris nenurodytas");
        else if (!IbanValidator.IsValid(bankAccount))
            result.Add("Neteisingas banko sąskaitos numeris");

        if (defaultVatRate == 6m)
        {
            if (string.IsNullOrWhiteSpace(compensationVatCode))
                result.Add("Trūksta kompensacinio PVM tarifo kodo");
            else if (CompensationVatCodeValidator.IsObsoleteFormat(compensationVatCode))
                result.Add("Kompensacinio PVM tarifo kodas yra pasenusio 13 skaitmenų formato — reikia perregistruoti");
            else if (!CompensationVatCodeValidator.IsValid(compensationVatCode))
                result.Add("Neteisingas kompensacinio PVM tarifo kodas");
        }

        return result;
    }
}
