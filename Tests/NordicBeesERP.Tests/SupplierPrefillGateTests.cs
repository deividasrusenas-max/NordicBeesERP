using System.Reflection;
using NordicBeesERP.Components.Dialogs;
using NordicBeesERP.Helpers;
using NordicBeesERP.Models;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// OCR Etapas 1 S5(d), D-038 Q5 — the supplier-create dialog does not prefill an invalid IBAN or VAT code from
/// the invoice: the field stays empty and a Lithuanian warning tells the user to type it in. The gate is pure
/// (<see cref="SupplierPrefillGate"/>); the dialog test runs the real <c>OnParametersSetAsync</c> by reflection
/// (no renderer needed — that method uses no injected service).
/// </summary>
public class SupplierPrefillGateTests
{
    private const string ValidLtIban = "LT121000011101001000";

    // ---------- gate (pure) ----------

    [Theory]
    [InlineData("LT12100001110100100")]      // wrong length
    [InlineData("LT35100001110100100")]      // wrong length (19, LT needs 20) but mod-97 valid: only the per-country length table catches it
    [InlineData("LT12100001110100100$")]     // bad character
    [InlineData("LT131000011101001000")]     // checksum
    public void Iban_Invalid_NotPrefilled_WithWarning(string iban)
    {
        var result = SupplierPrefillGate.CheckIban(iban);

        Assert.Equal("", result.Value);
        Assert.Equal("Sąskaitoje nurodytas IBAN neteisingas — įveskite ranka", result.Warning);
    }

    [Theory]
    [InlineData(ValidLtIban)]
    [InlineData("NO9386011117947")]   // valid, country not in the length table
    [InlineData("")]
    [InlineData(null)]
    public void Iban_ValidUnknownOrEmpty_PassedThrough(string? iban)
    {
        var result = SupplierPrefillGate.CheckIban(iban);

        Assert.Equal(iban, result.Value);
        Assert.Null(result.Warning);
    }

    [Theory]
    [InlineData("LT12345", "LT")]
    [InlineData("123456789", "LV")]
    public void VatCode_WrongFormat_NotPrefilled_WithWarning(string vat, string hint)
    {
        var result = SupplierPrefillGate.CheckVatCode(vat, hint);

        Assert.Null(result.Value);
        Assert.Equal("Sąskaitoje nurodytas PVM kodas neteisingas — įveskite ranka", result.Warning);
    }

    [Theory]
    [InlineData("LT123456789", "LT")]
    [InlineData("GB123456789", "GB")]       // unknown country: not judged, passed through
    [InlineData("LV12345678901", "LT")]     // country mismatch: not a format error
    [InlineData("", "LT")]
    [InlineData(null, null)]
    public void VatCode_OtherOutcomes_PassedThrough(string? vat, string? hint)
    {
        var result = SupplierPrefillGate.CheckVatCode(vat, hint);

        Assert.Equal(vat, result.Value);
        Assert.Null(result.Warning);
    }

    // ---------- the real dialog ----------

    private static async Task<(Supplier Supplier, List<string> Warnings)> PrefillAsync(string? vat, string? iban, string country = "LT")
    {
        var dialog = new SupplierCreateDialog { PrefilledName = "OCR Tiekėjas", PrefilledVatCode = vat, PrefilledBankAccount = iban, PrefilledCountryCode = country };
        const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        var method = typeof(SupplierCreateDialog).GetMethod("OnParametersSetAsync", any)!;
        await (Task)method.Invoke(dialog, null)!;
        var supplier = (Supplier)typeof(SupplierCreateDialog).GetField("supplier", any)!.GetValue(dialog)!;
        var warnings = (List<string>)typeof(SupplierCreateDialog).GetField("_prefillWarnings", any)!.GetValue(dialog)!;
        return (supplier, warnings);
    }

    [Fact]
    public async Task Dialog_InvalidIbanAndVat_FieldsEmpty_TwoWarnings()
    {
        var (supplier, warnings) = await PrefillAsync("LT12345", "LT131000011101001000");

        Assert.True(string.IsNullOrEmpty(supplier.VatCode));
        Assert.Equal("", supplier.BankAccount);
        Assert.Contains("Sąskaitoje nurodytas IBAN neteisingas — įveskite ranka", warnings);
        Assert.Contains("Sąskaitoje nurodytas PVM kodas neteisingas — įveskite ranka", warnings);
        Assert.Equal("OCR Tiekėjas", supplier.Name); // the rest of the prefill is untouched
    }

    [Fact]
    public async Task Dialog_ValidCodes_Prefilled_NoWarnings()
    {
        var (supplier, warnings) = await PrefillAsync("LT123456789", ValidLtIban);

        Assert.Equal("LT123456789", supplier.VatCode);
        Assert.Equal(ValidLtIban, supplier.BankAccount);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task Dialog_OnlyIbanInvalid_VatKept()
    {
        var (supplier, warnings) = await PrefillAsync("LT123456789", "LT12100001110100100");

        Assert.Equal("LT123456789", supplier.VatCode);
        Assert.Equal("", supplier.BankAccount);
        Assert.Equal(new[] { SupplierPrefillGate.InvalidIbanWarning }, warnings);
    }
}
