using System.Globalization;
using System.Text;

namespace NordicBeesERP.Helpers
{
    /// <summary>
    /// Folds diacritic characters (Lithuanian ą č ę ė į š ų ū ž, German ä ö ü ß) to
    /// their base Latin letters and lowercases the result, for use in
    /// diacritic- and case-insensitive text matching.
    /// Examples: "Žūklinė" → "zukline", "München" → "munchen", "Straße" → "strasse".
    /// </summary>
    public static class DiacriticHelper
    {
        public static string Fold(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Decompose (e.g. "ą" → "a" + combining ogonek), strip combining marks, re-compose.
            var decomposed = input.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }

            var folded = sb.ToString().Normalize(NormalizationForm.FormC);

            // "ß" is a single code point and is NOT decomposed by FormD — fold explicitly.
            // OrdinalIgnoreCase also matches "ẞ" (capital sharp s).
            folded = folded.Replace("ß", "ss", StringComparison.OrdinalIgnoreCase);

            return folded.ToLowerInvariant();
        }
    }
}
