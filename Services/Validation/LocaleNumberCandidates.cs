using System.Globalization;
using System.Text.RegularExpressions;

namespace NordicBeesERP.Services.Validation;

/// <summary>How the typed value Azure returned relates to the printed text it came from.</summary>
public enum NumberReadOutcome
{
    /// <summary>The printed text has exactly one strict reading, and Azure's value equals it.</summary>
    Match,

    /// <summary>Azure's value is not any strict reading of the printed text — it is wrong.</summary>
    Misread,

    /// <summary>The printed text has several strict readings (e.g. „9,000" = 9 or 9000) and Azure's value is one of them.</summary>
    Ambiguous,

    /// <summary>Nothing to compare: no printed text, no value, or the text has no strict reading at all (e.g. „1,1.1").</summary>
    NotCheckable
}

/// <summary>Result of <see cref="LocaleNumberCandidates.Check"/>. <see cref="Candidates"/> is ascending and distinct.</summary>
public sealed record NumberReadResult(NumberReadOutcome Outcome, IReadOnlyList<decimal> Candidates);

/// <summary>
/// Detects locale misreads of printed numbers (PLAN-ETAPAS1 §3.2 steps 1–2, D-038 Q6: detection only,
/// values are never replaced). Pure static, no I/O — OCR Etapas 1 prep (S1d), NOT wired in anywhere.
/// <para>
/// Azure DI returns typed numbers (<c>valueNumber</c>, <c>valueCurrency.amount</c>) next to the printed
/// <c>content</c>; on ASF0021438 „3 888,000" came back as 3 and „9,000" as 9000. This class parses the
/// printed text strictly under the two conventions that occur on our invoices and compares:
/// </para>
/// <list type="bullet">
/// <item>decimal comma — thousands separated by a space (incl. no-break and thin spaces), a dot, or not at all;</item>
/// <item>decimal point — thousands separated by a space, a comma, or not at all.</item>
/// </list>
/// <para>
/// Grouping is strict (RESEARCH §3: .NET's own parser does not check it): the first group has 1–3
/// digits and does not start with 0, every further group exactly 3, and one kind of separator per
/// number. A leading „-", „−" or „+" is the sign; letters, currency symbols and „%" at either end
/// are ignored („1 130,40 €"). Anything else — two decimal separators, parentheses, a trailing
/// sign — has no strict reading and is <see cref="NumberReadOutcome.NotCheckable"/>.
/// </para>
/// </summary>
public static class LocaleNumberCandidates
{
    private const string Space = "[    ]";

    // (integer part, fraction) per convention and grouping; the fraction group is optional.
    private static readonly Regex[] Conventions =
    {
        new(@"^(\d+)(?:,(\d+))?$"),                                  // decimal comma, no grouping
        new(@"^([1-9]\d{0,2}(?:" + Space + @"\d{3})+)(?:,(\d+))?$"),  // decimal comma, space grouping
        new(@"^([1-9]\d{0,2}(?:\.\d{3})+)(?:,(\d+))?$"),              // decimal comma, dot grouping
        new(@"^(\d+)(?:\.(\d+))?$"),                                 // decimal point, no grouping
        new(@"^([1-9]\d{0,2}(?:" + Space + @"\d{3})+)(?:\.(\d+))?$"), // decimal point, space grouping
        new(@"^([1-9]\d{0,2}(?:,\d{3})+)(?:\.(\d+))?$"),              // decimal point, comma grouping
    };

    /// <summary>
    /// Every strict reading of <paramref name="printed"/>, ascending and distinct; empty when there is
    /// none. Never throws.
    /// </summary>
    public static IReadOnlyList<decimal> Parse(string? printed)
    {
        if (string.IsNullOrWhiteSpace(printed)) return Array.Empty<decimal>();

        var text = TrimDecoration(printed);
        var negative = false;
        if (text.Length > 0 && (text[0] == '-' || text[0] == '−' || text[0] == '+'))
        {
            negative = text[0] != '+';
            text = TrimDecoration(text[1..]);
        }
        if (text.Length == 0) return Array.Empty<decimal>();

        var values = new SortedSet<decimal>();
        foreach (var convention in Conventions)
        {
            var m = convention.Match(text);
            if (!m.Success) continue;

            var digits = new string(m.Groups[1].Value.Where(char.IsAsciiDigit).ToArray());
            var fraction = m.Groups[2].Success ? "." + m.Groups[2].Value : "";
            if (decimal.TryParse(digits + fraction, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                values.Add(negative ? -value : value);
        }
        return values.ToList();
    }

    /// <summary>
    /// Compares Azure's typed <paramref name="azureValue"/> with the strict readings of
    /// <paramref name="printed"/>. A value outside the readings is <see cref="NumberReadOutcome.Misread"/>
    /// even when the text is ambiguous. Never throws.
    /// </summary>
    public static NumberReadResult Check(string? printed, decimal? azureValue)
    {
        var candidates = Parse(printed);
        if (azureValue is null || candidates.Count == 0)
            return new NumberReadResult(NumberReadOutcome.NotCheckable, candidates);

        if (!candidates.Any(c => SameNumber(c, azureValue.Value)))
            return new NumberReadResult(NumberReadOutcome.Misread, candidates);

        return new NumberReadResult(candidates.Count == 1 ? NumberReadOutcome.Match : NumberReadOutcome.Ambiguous, candidates);
    }

    // Azure's numbers pass through double; allow the conversion noise, nothing more.
    private static bool SameNumber(decimal candidate, decimal azure)
    {
        try
        {
            return Math.Abs(candidate - azure) <= 1e-9m * Math.Max(1m, Math.Abs(candidate));
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string TrimDecoration(string s)
    {
        int start = 0, end = s.Length;
        while (start < end && IsDecoration(s[start])) start++;
        while (end > start && IsDecoration(s[end - 1])) end--;
        return s[start..end];
    }

    private static bool IsDecoration(char c) =>
        char.IsWhiteSpace(c) || char.IsLetter(c) || c == '%'
        || char.GetUnicodeCategory(c) == UnicodeCategory.CurrencySymbol;
}
