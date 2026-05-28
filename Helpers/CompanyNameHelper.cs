using System;
using System.Collections.Generic;

namespace NordicBeesERP.Helpers
{
    public static class CompanyNameHelper
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Lithuanian
                { "Uždaroji akcinė bendrovė", "UAB" },
                { "Mažoji bendrija", "MB" },
                { "Akcinė bendrovė", "AB" },
                { "Individuali įmonė", "IĮ" },
                { "Viešoji įstaiga", "VšĮ" },
                { "Žemės ūkio bendrovė", "ŽŪB" },
                { "Kooperatinė bendrovė", "KB" },
                // German
                { "Gesellschaft mit beschränkter Haftung", "GmbH" },
                { "Aktiengesellschaft", "AG" },
                // French
                { "Société à responsabilité limitée", "SARL" },
                { "Société anonyme", "SA" },
                // English
                { "Limited liability company", "LLC" },
                { "Limited", "Ltd" },
                // Polish
                { "Spółka z ograniczoną odpowiedzialnością", "Sp. z o.o." },
                // Latvian
                { "Sabiedrība ar ierobežotu atbildību", "SIA" },
                // Estonian
                { "Osaühing", "OÜ" },
            };

            var result = name.Trim();
            foreach (var entry in replacements)
            {
                var full = entry.Key;
                var shortName = entry.Value;
                // Match at start: "Uždaroji akcinė bendrovė Rotoma" → "UAB Rotoma"
                if (result.StartsWith(full + " ", StringComparison.OrdinalIgnoreCase))
                    result = shortName + " " + result.Substring(full.Length).Trim();
                // Match with quotes: "Uždaroji akcinė bendrovė „Rotoma"" → "UAB „Rotoma""
                else if (result.StartsWith(full, StringComparison.OrdinalIgnoreCase) && result.Length > full.Length)
                    result = shortName + result.Substring(full.Length);
            }

            // Clean up quotes around company name: UAB "Rotoma" → UAB Rotoma  (optional - keep quotes)
            return result.Trim();
        }
    }
}