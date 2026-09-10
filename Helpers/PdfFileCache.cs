namespace NordicBeesERP.Helpers;

public static class PdfFileCache
{
    /// <summary>
    /// Reads a previously cached PDF file, given the base directory and the
    /// year-relative path stored in the DB (e.g. "2026/INV-0001.pdf").
    /// Returns null when the path is null/empty, not a safe relative path, or
    /// the file does not exist — the caller then falls back to regeneration.
    /// </summary>
    public static byte[]? TryRead(string baseDir, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        if (Path.IsPathRooted(relativePath)) return null;
        if (relativePath.Contains("..")) return null;   // path-traversal guard: DB-stored value
        var fullPath = Path.Combine(baseDir, relativePath);
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
}
