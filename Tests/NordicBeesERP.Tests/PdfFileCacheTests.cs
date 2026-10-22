using NordicBeesERP.Helpers;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>Pure filesystem tests for PdfFileCache (no database).</summary>
public class PdfFileCacheTests : IDisposable
{
    private readonly string _baseDir = Path.Combine(Path.GetTempPath(), $"npb-cache-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_baseDir)) Directory.Delete(_baseDir, recursive: true);
    }

    private static readonly byte[] Bytes = { 0x25, 0x50, 0x44, 0x46 };

    [Fact]
    public void TryDelete_ExistingFile_RemovesItAndReturnsTrue()
    {
        var rel = PdfFileCache.Save(_baseDir, 2026, "A.pdf", Bytes);

        Assert.True(PdfFileCache.TryDelete(_baseDir, rel));
        Assert.False(File.Exists(Path.Combine(_baseDir, rel)));
    }

    [Fact]
    public void TryDelete_MissingFileOrNullPath_ReturnsFalseWithoutThrowing()
    {
        Assert.False(PdfFileCache.TryDelete(_baseDir, "2026/missing.pdf"));
        Assert.False(PdfFileCache.TryDelete(_baseDir, null));
        Assert.False(PdfFileCache.TryDelete(_baseDir, "  "));
    }

    [Fact]
    public void TryDelete_UnsafePath_NeverDeletesOutsideBaseDir()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"npb-outside-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(outside, Bytes);
        try
        {
            Assert.False(PdfFileCache.TryDelete(_baseDir, outside));                 // rooted
            Assert.False(PdfFileCache.TryDelete(_baseDir, $"../{Path.GetFileName(outside)}")); // traversal
            Assert.True(File.Exists(outside));
        }
        finally { File.Delete(outside); }
    }

    [Fact]
    public void TryDelete_ParentIsAFile_DoesNotThrow()
    {
        Directory.CreateDirectory(_baseDir);
        File.WriteAllBytes(Path.Combine(_baseDir, "2026"), Bytes);

        var ex = Record.Exception(() => PdfFileCache.TryDelete(_baseDir, "2026/x.pdf"));

        Assert.Null(ex);
    }

    [Fact]
    public void TryDelete_IoFailure_IsLoggedNotThrown()
    {
        if (OperatingSystem.IsWindows()) return; // relies on a read-only directory
        var yearDir = Path.Combine(_baseDir, "2026");
        Directory.CreateDirectory(yearDir);
        File.WriteAllBytes(Path.Combine(yearDir, "locked.pdf"), Bytes);
        File.SetUnixFileMode(yearDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var logger = new CapturingLogger();
        try
        {
            var deleted = PdfFileCache.TryDelete(_baseDir, "2026/locked.pdf", logger);

            if (!deleted) // running as root can still delete; then there is nothing to log
            {
                Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning && e.Message.Contains("2026/locked.pdf"));
            }
        }
        finally { File.SetUnixFileMode(yearDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
    }

    private sealed class CapturingLogger : Microsoft.Extensions.Logging.ILogger
    {
        public List<(Microsoft.Extensions.Logging.LogLevel Level, string Message)> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public void IsStale_FileOlderThanChange_ReturnsTrue()
    {
        var rel = PdfFileCache.Save(_baseDir, 2026, "B.pdf", Bytes);
        var mtime = File.GetLastWriteTimeUtc(Path.Combine(_baseDir, rel));

        Assert.True(PdfFileCache.IsStale(_baseDir, rel, mtime.AddSeconds(1)));
    }

    [Fact]
    public void IsStale_FileNewerOrEqualToChange_ReturnsFalse()
    {
        var rel = PdfFileCache.Save(_baseDir, 2026, "C.pdf", Bytes);
        var mtime = File.GetLastWriteTimeUtc(Path.Combine(_baseDir, rel));

        Assert.False(PdfFileCache.IsStale(_baseDir, rel, mtime));
        Assert.False(PdfFileCache.IsStale(_baseDir, rel, mtime.AddSeconds(-1)));
    }

    [Fact]
    public void IsStale_NoChangeTimestampMissingFileOrUnsafePath_ReturnsFalse()
    {
        var rel = PdfFileCache.Save(_baseDir, 2026, "D.pdf", Bytes);

        Assert.False(PdfFileCache.IsStale(_baseDir, rel, null));
        Assert.False(PdfFileCache.IsStale(_baseDir, "2026/missing.pdf", DateTime.UtcNow.AddDays(1)));
        Assert.False(PdfFileCache.IsStale(_baseDir, "../x.pdf", DateTime.UtcNow.AddDays(1)));
    }
}
