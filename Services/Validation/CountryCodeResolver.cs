using System.Globalization;
using System.Text;

namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Supplier country as an ISO 3166-1 alpha-2 code, never a truncated name (D-042). Pure, no I/O.
/// Priority in <see cref="Resolve"/>: a well-formed VAT prefix (EL → GR), then an ISO code returned by Azure,
/// then an explicit country-name map (native, English, Lithuanian; case- and diacritic-insensitive),
/// otherwise null. A name is never cut to two letters, and a two-letter value that is not a valid ISO code
/// is never returned.
/// </summary>
public static class CountryCodeResolver
{
    private static readonly HashSet<string> IsoCodes = new(StringComparer.Ordinal)
    {
        "AD","AE","AF","AG","AI","AL","AM","AO","AQ","AR","AS","AT","AU","AW","AX","AZ",
        "BA","BB","BD","BE","BF","BG","BH","BI","BJ","BL","BM","BN","BO","BQ","BR","BS","BT","BV","BW","BY","BZ",
        "CA","CC","CD","CF","CG","CH","CI","CK","CL","CM","CN","CO","CR","CU","CV","CW","CX","CY","CZ",
        "DE","DJ","DK","DM","DO","DZ",
        "EC","EE","EG","EH","ER","ES","ET",
        "FI","FJ","FK","FM","FO","FR",
        "GA","GB","GD","GE","GF","GG","GH","GI","GL","GM","GN","GP","GQ","GR","GS","GT","GU","GW","GY",
        "HK","HM","HN","HR","HT","HU",
        "ID","IE","IL","IM","IN","IO","IQ","IR","IS","IT",
        "JE","JM","JO","JP",
        "KE","KG","KH","KI","KM","KN","KP","KR","KW","KY","KZ",
        "LA","LB","LC","LI","LK","LR","LS","LT","LU","LV","LY",
        "MA","MC","MD","ME","MF","MG","MH","MK","ML","MM","MN","MO","MP","MQ","MR","MS","MT","MU","MV","MW","MX","MY","MZ",
        "NA","NC","NE","NF","NG","NI","NL","NO","NP","NR","NU","NZ",
        "OM",
        "PA","PE","PF","PG","PH","PK","PL","PM","PN","PR","PS","PT","PW","PY",
        "QA",
        "RE","RO","RS","RU","RW",
        "SA","SB","SC","SD","SE","SG","SH","SI","SJ","SK","SL","SM","SN","SO","SR","SS","ST","SV","SX","SY","SZ",
        "TC","TD","TF","TG","TH","TJ","TK","TL","TM","TN","TO","TR","TT","TV","TW","TZ",
        "UA","UG","UM","US","UY","UZ",
        "VA","VC","VE","VG","VI","VN","VU",
        "WF","WS",
        "YE","YT",
        "ZA","ZM","ZW"
    };

    // Names are stored folded (see Fold): lower case, no diacritics, words separated by single spaces.
    private static readonly Dictionary<string, string> NameMap = BuildNameMap();

    private static Dictionary<string, string> BuildNameMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string code, params string[] names)
        {
            foreach (var name in names) map[Fold(name)] = code;
        }

        Add("LT", "Lietuva", "Lithuania", "Litauen", "Lituania", "Lituanie", "Litva");
        Add("LV", "Latvija", "Latvia", "Lettland", "Letonia", "Lettonie", "Latvija");
        Add("EE", "Eesti", "Estonia", "Estija", "Estland", "Estonie");
        Add("DE", "Deutschland", "Germany", "Vokietija", "Allemagne", "Alemania", "Germania", "Niemcy");
        Add("PL", "Polska", "Poland", "Lenkija", "Polen", "Pologne", "Polonia");
        Add("RO", "Romania", "România", "Rumunija", "Rumänien", "Roumanie", "Rumunia");
        Add("CZ", "Česko", "Czechia", "Czech Republic", "Česká republika", "Čekija", "Čekijos Respublika", "Tschechien", "Cehia");
        Add("ES", "España", "Spain", "Ispanija", "Spanien", "Espagne");
        Add("IE", "Ireland", "Éire", "Eire", "Airija", "Irland", "Irlande", "Ierland", "Republic of Ireland");
        Add("NL", "Nederland", "Netherlands", "The Netherlands", "Nyderlandai", "Olandija", "Holland", "Niederlande", "Pays-Bas");
        Add("GB", "United Kingdom", "UK", "Great Britain", "Britain", "England", "Jungtinė Karalystė", "Didžioji Britanija", "Anglija", "Vereinigtes Königreich");
        Add("UA", "Ukraina", "Ukraine", "Україна");
        Add("FI", "Suomi", "Finland", "Suomija", "Finnland", "Finlande");
        Add("SE", "Sverige", "Sweden", "Švedija", "Schweden", "Suède");
        Add("DK", "Danmark", "Denmark", "Danija", "Dänemark", "Danemark");
        Add("FR", "France", "Prancūzija", "Frankreich", "Francia");
        Add("IT", "Italia", "Italy", "Italija", "Italien", "Italie");
        Add("AT", "Österreich", "Austria", "Austrija", "Autriche");
        Add("BE", "België", "Belgique", "Belgium", "Belgija", "Belgien");
        Add("GR", "Greece", "Hellas", "Ellada", "Graikija", "Griechenland", "Grèce", "Grecia");
        Add("NO", "Norge", "Norway", "Norvegija", "Norwegen");
        Add("CH", "Schweiz", "Suisse", "Switzerland", "Šveicarija", "Svizzera");
        Add("SK", "Slovensko", "Slovakia", "Slovakija", "Slowakei");
        Add("HU", "Magyarország", "Hungary", "Vengrija", "Ungarn");
        Add("BG", "Bulgaria", "Bulgarija", "Bulgarien", "България");
        Add("PT", "Portugal", "Portugalija");
        Add("HR", "Hrvatska", "Croatia", "Kroatija");
        Add("SI", "Slovenija", "Slovenia", "Slovėnija", "Slowenien");
        Add("LU", "Luxembourg", "Luxemburg", "Liuksemburgas", "Lëtzebuerg");
        Add("LI", "Liechtenstein", "Lichtenšteinas");
        Add("US", "United States", "United States of America", "USA", "JAV", "Jungtinės Amerikos Valstijos");
        return map;
    }

    /// <summary>True when <paramref name="code"/> is an ISO 3166-1 alpha-2 code (case-insensitive, trimmed).</summary>
    public static bool IsValidIso(string? code) =>
        code is not null && code.Trim().ToUpperInvariant() is { Length: 2 } upper && IsoCodes.Contains(upper);

    /// <summary>
    /// A country name in any supported language mapped to its ISO code; null when the name is not in the map.
    /// Never derives a code from the letters of the name.
    /// </summary>
    public static string? FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return NameMap.TryGetValue(Fold(name), out var code) ? code : null;
    }

    /// <summary>
    /// The country an address field names — an ISO code as returned by Azure, or a country name. A two-letter
    /// value that is not a valid ISO code is looked up as a name (UK → GB) and otherwise dropped. Null when unknown.
    /// </summary>
    public static string? FromAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length == 2 && IsValidIso(trimmed)) return trimmed.ToUpperInvariant();
        return FromName(trimmed);
    }

    /// <summary>
    /// The country of a well-formed VAT prefix: two letters that are an ISO code (EL → GR) followed by a
    /// non-empty alphanumeric body, after whitespace, dots and dashes are removed. A code without a prefix,
    /// with a prefix outside ISO (XX, XI), or with nothing after the prefix gives null.
    /// </summary>
    public static string? FromVatPrefix(string? vatCode)
    {
        if (string.IsNullOrWhiteSpace(vatCode)) return null;
        var normalized = new string(vatCode.Where(c => !char.IsWhiteSpace(c) && c is not ('.' or '-' or '–' or '—')).ToArray())
            .ToUpperInvariant();
        if (normalized.Length < 3) return null;
        if (!IsAsciiLetter(normalized[0]) || !IsAsciiLetter(normalized[1])) return null;
        if (!normalized.Skip(2).All(c => IsAsciiLetter(c) || char.IsAsciiDigit(c))) return null;

        var prefix = normalized[..2];
        if (prefix == "EL") return "GR";
        return IsoCodes.Contains(prefix) ? prefix : null;
    }

    /// <summary>
    /// D-042 priority: a well-formed VAT prefix, then the address country (an ISO code or a mapped name),
    /// otherwise null. The result is always a valid ISO code or null.
    /// </summary>
    public static string? Resolve(string? vatCode, string? addressCountry) =>
        FromVatPrefix(vatCode) ?? FromAddress(addressCountry);

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z';

    /// <summary>Lower case, diacritics removed, punctuation and runs of whitespace collapsed to single spaces.</summary>
    private static string Fold(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Replace("ß", "ss").Replace("ł", "l").Replace("ø", "o")
            .Replace("æ", "ae").Replace("đ", "d").Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
