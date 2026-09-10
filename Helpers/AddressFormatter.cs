namespace NordicBeesERP.Helpers;

public static class AddressFormatter
{
    /// <summary>
    /// Joins address/postal code/city/country into one printable line,
    /// skipping empty parts. Matches the pattern already used in
    /// DebtReconciliationService and UnpaidInvoicesService.
    /// </summary>
    public static string FormatFull(string? address, string? postalCode, string? city, string? country)
    {
        var parts = new[] { address, postalCode, city, country }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim());
        return string.Join(", ", parts);
    }
}
