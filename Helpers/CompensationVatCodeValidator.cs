using System.Linq;

namespace NordicBeesERP.Helpers;

public static class CompensationVatCodeValidator
{
    private static readonly int[] FirstPassWeights = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1, 2 };
    private static readonly int[] SecondPassWeights = { 3, 4, 5, 6, 7, 8, 9, 1, 2, 3, 4 };

    public static bool IsValid(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (code.Length == 13) return false; // obsolete pre-2013 format — explicitly rejected
        if (code.Length != 12) return false;
        if (!code.All(char.IsDigit)) return false;
        if (code[10] != '2') return false; // index digit must be '2'

        var sum = 0;
        for (var i = 0; i < 11; i++)
            sum += (code[i] - '0') * FirstPassWeights[i];
        var remainder = sum % 11;

        int expectedCheckDigit;
        if (remainder != 10)
        {
            expectedCheckDigit = remainder;
        }
        else
        {
            var secondSum = 0;
            for (var i = 0; i < 11; i++)
                secondSum += (code[i] - '0') * SecondPassWeights[i];
            var secondRemainder = secondSum % 11;
            expectedCheckDigit = secondRemainder == 10 ? 0 : secondRemainder;
        }

        return code[11] - '0' == expectedCheckDigit;
    }

    /// <summary>
    /// Returns a specific human-readable Lithuanian message when a code is
    /// the obsolete pre-2013 13-digit format, so callers can distinguish
    /// "wrong/invalid code" from "outdated format, needs re-registration".
    /// </summary>
    public static bool IsObsoleteFormat(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length == 13 && code.All(char.IsDigit);
}
