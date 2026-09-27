namespace NordicBeesERP.Services.Validation;

/// <summary>
/// Ranks partners by how much their name looks like a document's supplier name (PLAN-ETAPAS2 §1.6, D-044 Q3, S6). Used ONLY to order the
/// „Priskirti esamam" / „Pakeisti tiekėją" pickers so the human sees the likely partner first — it never assigns and never feeds the
/// matcher. Pure: token-sort Jaro-Winkler on the normalised names (<see cref="SupplierIdentityNormalizer.NameNormalized"/>: diacritics
/// folded, punctuation and legal forms dropped) blended with the token overlap, so "UAB Rotoma" and "UAB Rotoma Plius" are close but
/// never equal. No third-party package.
/// </summary>
public static class SupplierNameRanker
{
    private const double JaroWinklerWeight = 0.7;
    private const double TokenOverlapWeight = 0.3;

    /// <summary>Similarity of two supplier names in [0, 1]: 1 only when their normalised token sets are identical; 0 when either has no usable name.</summary>
    public static double Score(string? a, string? b)
    {
        var left = Tokens(a);
        var right = Tokens(b);
        if (left.Count == 0 || right.Count == 0) return 0;

        var sortedLeft = string.Join(' ', left.OrderBy(t => t, StringComparer.Ordinal));
        var sortedRight = string.Join(' ', right.OrderBy(t => t, StringComparer.Ordinal));
        if (sortedLeft == sortedRight) return 1;

        var intersection = left.Intersect(right).Count();
        var union = left.Union(right).Count();
        var overlap = union == 0 ? 0 : (double)intersection / union;

        // strictly below 1 unless the sets are identical: a near-perfect string similarity never rounds up to "equal"
        return Math.Min(0.999, JaroWinklerWeight * JaroWinkler(sortedLeft, sortedRight) + TokenOverlapWeight * overlap);
    }

    /// <summary>
    /// The items ordered for a picker: those <paramref name="isPriority"/> first (the matcher's Suggested / Ambiguous candidates), then by
    /// descending similarity to <paramref name="documentName"/>, then by name — deterministic. Empty document name: priority, then name.
    /// </summary>
    public static IReadOnlyList<T> Order<T>(IEnumerable<T> items, Func<T, string?> nameOf, string? documentName, Func<T, bool>? isPriority = null) =>
        items
            .Select(item => (Item: item, Name: nameOf(item) ?? "", Priority: isPriority?.Invoke(item) == true, Score: Score(documentName, nameOf(item))))
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => x.Item)
            .ToList();

    /// <summary>Jaro-Winkler similarity in [0, 1] (prefix scale 0.1, at most 4 prefix characters, boost from Jaro 0.7).</summary>
    public static double JaroWinkler(string a, string b)
    {
        var jaro = Jaro(a, b);
        if (jaro < 0.7) return jaro;
        var prefix = 0;
        while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix]) prefix++;
        return jaro + prefix * 0.1 * (1 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a == b) return 1;

        var window = Math.Max(Math.Max(a.Length, b.Length) / 2 - 1, 0);
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        var matches = 0;
        for (var i = 0; i < a.Length; i++)
        {
            var from = Math.Max(0, i - window);
            var to = Math.Min(b.Length - 1, i + window);
            for (var j = from; j <= to; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = bMatched[j] = true;
                matches++;
                break;
            }
        }
        if (matches == 0) return 0;

        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }
        return ((double)matches / a.Length + (double)matches / b.Length + (matches - transpositions / 2.0) / matches) / 3;
    }

    private static List<string> Tokens(string? name) =>
        SupplierIdentityNormalizer.NameNormalized(name).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
}
