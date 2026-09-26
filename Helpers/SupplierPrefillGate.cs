using NordicBeesERP.Services.Validation;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Decides what the supplier-create dialog may prefill from an invoice's OCR data (OCR Etapas 1 S5(d),
/// D-038 Q5): a code that fails its format check would enter master data, so the field stays empty and the
/// user gets a warning to type it in. Only failures count — an unknown country, a country mismatch, an
/// empty value or an IBAN of a country outside the length table are passed through unchanged. Pure, no I/O.
/// </summary>
public static class SupplierPrefillGate
{
    public const string InvalidIbanWarning = "Sąskaitoje nurodytas IBAN neteisingas — įveskite ranka";
    public const string InvalidVatCodeWarning = "Sąskaitoje nurodytas PVM kodas neteisingas — įveskite ranka";

    /// <summary><see cref="Value"/> is what may be prefilled; <see cref="Warning"/> is set when the input was rejected.</summary>
    public sealed record Result(string? Value, string? Warning);

    public static Result CheckIban(string? iban)
    {
        var reason = NordicBeesERP.Services.Validation.IbanValidator.Validate(iban).Reason;
        return reason is IbanValidationReason.WrongLength or IbanValidationReason.BadCharacters or IbanValidationReason.ChecksumFailed
            ? new Result("", InvalidIbanWarning)
            : new Result(iban, null);
    }

    public static Result CheckVatCode(string? vatCode, string? countryHint) =>
        VatCodeFormatValidator.Validate(vatCode, countryHint).Reason == VatCodeValidationReason.WrongFormat
            ? new Result(null, InvalidVatCodeWarning)
            : new Result(vatCode, null);
}
