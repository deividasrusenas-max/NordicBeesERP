namespace NordicBeesERP.Helpers;

public static class FilterUrlBuilder
{
    // Builds a query string from named key-value pairs, omitting
    // any null/empty values. Returns basePath unchanged if all
    // filters are empty.
    public static string Build(string basePath, params (string Key, string? Value)[] filters)
    {
        var parts = filters
            .Where(f => !string.IsNullOrEmpty(f.Value))
            .Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value!)}");
        var query = string.Join("&", parts);
        return query.Length > 0 ? $"{basePath}?{query}" : basePath;
    }

    // Value-to-query-string conversion helpers (return null to omit)
    public static string? ToQueryValue(DateTime? date) =>
        date?.ToString("yyyy-MM-dd");

    public static string? ToQueryValue(bool? flag) =>
        flag.HasValue ? (flag.Value ? "1" : "0") : null;

    public static string? ToQueryValue(int? value) =>
        value?.ToString();

    public static string? ToQueryValue(IEnumerable<string>? values) =>
        values != null && values.Any() ? string.Join(",", values) : null;

    // Query-string-to-value parse-back helpers
    public static bool? ParseBool(string? raw) =>
        raw switch { "1" => true, "0" => false, _ => null };

    public static HashSet<string> ParseStatusCsv(string? raw) =>
        string.IsNullOrEmpty(raw) ? new HashSet<string>() : new HashSet<string>(raw.Split(','));

    // Detail-page returnUrl pattern (used by list pages with drill-down)
    public static string BuildDetailUrl(string detailPath, string currentFullUri, string currentBaseUri)
    {
        var relativeCurrent = currentFullUri.Replace(currentBaseUri.TrimEnd('/'), "");
        return $"{detailPath}?returnUrl={Uri.EscapeDataString(relativeCurrent)}";
    }

    public static string ResolveReturnUrl(string? returnUrlParam, string fallbackPath) =>
        !string.IsNullOrEmpty(returnUrlParam) ? Uri.UnescapeDataString(returnUrlParam) : fallbackPath;
}
