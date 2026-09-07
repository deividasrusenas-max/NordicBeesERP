using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace NordicBeesERP.Helpers;

/// <summary>
/// Validates an IBAN (International Bank Account Number) using the ISO 7064
/// MOD-97-10 checksum algorithm. Pure static, no I/O. Accepts any valid EU
/// IBAN, not just LT — input may contain whitespace and mixed-case letters.
/// </summary>
public static partial class IbanValidator
{
    [GeneratedRegex("^[A-Za-z]{2}[0-9]{2}[A-Za-z0-9]+$")]
    private static partial Regex FormatRegex();

    /// <summary>
    /// Returns true when the input is a valid IBAN per ISO 7064 MOD-97-10:
    /// - not null/whitespace
    /// - after removing all whitespace, length is 15-34 characters and it
    ///   matches two letters + two digits + one or more alphanumerics
    /// - after moving the first 4 characters to the end and converting each
    ///   letter to its A=10..Z=35 digit pair, the resulting number mod 97 is 1.
    /// </summary>
    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban)) return false;

        var normalized = new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (normalized.Length < 15 || normalized.Length > 34) return false;
        if (!FormatRegex().IsMatch(normalized)) return false;

        var upper = normalized.ToUpperInvariant();
        var rearranged = upper[4..] + upper[..4];

        var numeric = new StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
        {
            if (char.IsDigit(c))
                numeric.Append(c);
            else
                numeric.Append(c - 'A' + 10);
        }

        return BigInteger.Parse(numeric.ToString()) % 97 == 1;
    }
}
