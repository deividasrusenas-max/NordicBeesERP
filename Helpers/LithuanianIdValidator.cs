using System.Linq;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Validates a Lithuanian asmens kodas (11-digit personal identification
/// number) using the standard checksum algorithm. Pure static, no I/O.
/// </summary>
public static class LithuanianIdValidator
{
    private static readonly int[] FirstPassWeights = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 1 };
    private static readonly int[] SecondPassWeights = { 3, 4, 5, 6, 7, 8, 9, 1, 2, 3 };

    /// <summary>
    /// Returns true when the input is a valid 11-digit asmens kodas:
    /// - exactly 11 characters, all digits
    /// - checksum computed over digits 1-10 with cyclic weights
    ///   1,2,3,4,5,6,7,8,9,1; if the first-pass remainder is 10,
    ///   recompute with cyclic weights 3,4,5,6,7,8,9,1,2,3; if that
    ///   remainder is also 10, the check digit is 0.
    /// </summary>
    public static bool IsValid(string? asmensKodas)
    {
        if (string.IsNullOrWhiteSpace(asmensKodas)) return false;
        if (asmensKodas.Length != 11) return false;
        if (!asmensKodas.All(char.IsDigit)) return false;

        var sum = 0;
        for (var i = 0; i < 10; i++)
            sum += (asmensKodas[i] - '0') * FirstPassWeights[i];
        var remainder = sum % 11;

        int expectedCheckDigit;
        if (remainder != 10)
        {
            expectedCheckDigit = remainder;
        }
        else
        {
            var secondSum = 0;
            for (var i = 0; i < 10; i++)
                secondSum += (asmensKodas[i] - '0') * SecondPassWeights[i];
            var secondRemainder = secondSum % 11;
            expectedCheckDigit = secondRemainder == 10 ? 0 : secondRemainder;
        }

        return asmensKodas[10] - '0' == expectedCheckDigit;
    }
}
