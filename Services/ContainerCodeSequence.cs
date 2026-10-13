using System.Text.RegularExpressions;

namespace NordicBeesERP.Services;

/// <summary>
/// Container codes derived from an operator-entered start ID ("Pradinis ID"). Restores the exact
/// <c>GenerateSequentialId</c> behaviour that DeliveryCreate had before commit ef1cefe.
/// </summary>
public static class ContainerCodeSequence
{
    /// <summary>"150" + 2 = "152"; "A-150" + 2 = "A-152" (prefix and zero padding kept); otherwise "start" / "start-(offset+1)".</summary>
    public static string Generate(string startId, int offset)
    {
        // Jei startId yra grynas skaičius: "150" + 2 = "152"
        if (int.TryParse(startId, out int numId))
            return (numId + offset).ToString();

        // Jei turi prefiksą + skaičių gale: "A-150" + 2 = "A-152"
        var match = Regex.Match(startId, @"^(.*?)(\d+)$");
        if (match.Success)
        {
            var prefix = match.Groups[1].Value;
            var number = int.Parse(match.Groups[2].Value);
            var format = new string('0', match.Groups[2].Value.Length);
            return $"{prefix}{(number + offset).ToString(format)}";
        }

        // Negalima generuoti sekos — grąžink originalą su prierašu
        return offset == 0 ? startId : $"{startId}-{offset + 1}";
    }

    /// <summary>Default start ID suggested from the last stored code: only a purely numeric last code yields last + 1.</summary>
    public static string SuggestStart(string? lastCode)
        => lastCode != null && int.TryParse(lastCode, out int lastNum) ? (lastNum + 1).ToString() : "";
}

/// <summary>Thrown when a container code in a delivery batch already exists (in the batch or in the DB).</summary>
public sealed class ContainerCodeConflictException : InvalidOperationException
{
    public IReadOnlyList<string> Codes { get; }

    public ContainerCodeConflictException(IReadOnlyList<string> codes)
        : base(codes.Count == 1 ? $"Kodas {codes[0]} jau egzistuoja" : $"Kodai {string.Join(", ", codes)} jau egzistuoja")
    {
        Codes = codes;
    }
}
