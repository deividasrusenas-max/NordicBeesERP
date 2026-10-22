using Microsoft.Extensions.Logging;

namespace NordicBeesERP.Helpers;

public static class PdfFileCache
{
    /// <summary>
    /// Resolves a DB-stored year-relative path under baseDir. Returns null when
    /// the path is null/empty or not a safe relative path (rooted or containing "..").
    /// </summary>
    private static string? ResolveSafe(string baseDir, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (Path.IsPathRooted(relativePath)) return null;
        if (relativePath.Contains("..")) return null;   // path-traversal guard: DB-stored value
        return Path.Combine(baseDir, relativePath);
    }

    /// <summary>
    /// Reads a previously cached PDF file, given the base directory and the
    /// year-relative path stored in the DB (e.g. "2026/INV-0001.pdf").
    /// Returns null when the path is null/empty, not a safe relative path, or
    /// the file does not exist — the caller then falls back to regeneration.
    /// </summary>
    public static byte[]? TryRead(string baseDir, string? relativePath)
    {
        var fullPath = ResolveSafe(baseDir, relativePath);
        if (fullPath == null) return null;
        if (!File.Exists(fullPath)) return null;
        return File.ReadAllBytes(fullPath);
    }

    /// <summary>
    /// Saves a PDF under baseDir/{year}/{fileName}, creating the year
    /// subdirectory if needed, and returns the year-relative path to store
    /// in the DB (e.g. "2026/INV-0001.pdf").
    /// </summary>
    public static string Save(string baseDir, int year, string fileName, byte[] pdfBytes)
    {
        var yearDir = Path.Combine(baseDir, year.ToString());
        Directory.CreateDirectory(yearDir);
        var fullPath = Path.Combine(yearDir, fileName);
        File.WriteAllBytes(fullPath, pdfBytes);
        return Path.Combine(year.ToString(), fileName);
    }

    /// <summary>
    /// Deletes a cached PDF. Never throws: a missing file or an unsafe path is a
    /// no-op, and an I/O failure is logged and reported as false (the cache entry
    /// is already invalidated in the DB, so a leftover file is harmless).
    /// Returns true only when a file was actually removed. The failure goes to <paramref name="logger"/> when given.
    /// </summary>
    public static bool TryDelete(string baseDir, string? relativePath, ILogger? logger = null)
    {
        try
        {
            var fullPath = ResolveSafe(baseDir, relativePath);
            if (fullPath == null || !File.Exists(fullPath)) return false;
            File.Delete(fullPath);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "PdfFileCache: nepavyko ištrinti '{RelativePath}'", relativePath);
            return false;
        }
    }

    /// <summary>
    /// True when the cached file is older than the newest content change
    /// (<paramref name="latestChangeUtc"/>, UTC) and so must be regenerated.
    /// False when there is no change timestamp, or the file is missing/unsafe
    /// (TryRead already returns null for those).
    /// </summary>
    public static bool IsStale(string baseDir, string? relativePath, DateTime? latestChangeUtc)
    {
        if (latestChangeUtc == null) return false;
        var fullPath = ResolveSafe(baseDir, relativePath);
        if (fullPath == null || !File.Exists(fullPath)) return false;
        return File.GetLastWriteTimeUtc(fullPath) < latestChangeUtc.Value;
    }
}
