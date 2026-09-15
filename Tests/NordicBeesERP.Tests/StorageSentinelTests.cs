#if !WINDOWS
using NordicBeesERP.Services.Storage;
using Xunit;

namespace NordicBeesERP.Tests;

/// <summary>
/// Unit tests for StorageSentinel (startup storage-volume check). Pure file-system
/// logic — no database, no fixtures. Each test uses its own fresh temp directory
/// under Path.GetTempPath() and deletes it in a finally block.
/// </summary>
public class StorageSentinelTests
{
    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "nordicbees-sentinel-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Fact]
    public void MissingSentinelInProductionThrows()
    {
        var root = NewRoot();
        try
        {
            var ex = Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Production"));

            // The message must name the sentinel path (the root at minimum).
            Assert.Contains(root, ex.Message);

            // Production must NEVER auto-create: the root stays empty.
            Assert.Empty(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MissingSentinelInStagingThrows()
    {
        var root = NewRoot();
        try
        {
            var ex = Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Staging"));

            Assert.Contains(root, ex.Message);

            // Staging must never auto-create either.
            Assert.Empty(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentMismatchThrows()
    {
        var root = NewRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, ".nordicbees-storage"), "Staging");

            var ex = Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Production"));

            Assert.Contains("Staging", ex.Message);
            Assert.Contains("Production", ex.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ContentComparisonIsCaseSensitive()
    {
        var root = NewRoot();
        try
        {
            // A lowercase sentinel must NOT satisfy a Production startup.
            File.WriteAllText(Path.Combine(root, ".nordicbees-storage"), "production");

            Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Production"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NonWritableRootThrows()
    {
        var root = NewRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, ".nordicbees-storage"), "Staging");

            // Read-only directory: the write check must fail with StorageSentinelException.
            File.SetUnixFileMode(root, UnixFileMode.UserRead);

            Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Staging"));
        }
        finally
        {
            // Restore writability BEFORE the recursive delete — a read-only directory
            // is not recursively deletable on macOS/Linux.
            try
            {
                File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch
            {
                // Best-effort: if restore fails, the delete below will surface it.
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DevelopmentAutoCreatesSentinel()
    {
        var root = NewRoot();
        try
        {
            var sentinelPath = Path.Combine(root, ".nordicbees-storage");

            // No exception; the sentinel is auto-created with exactly the environment name.
            StorageSentinel.EnsureStorageReady(root, "Development");
            Assert.True(File.Exists(sentinelPath));
            Assert.Equal("Development", File.ReadAllText(sentinelPath).Trim());

            // Idempotent: a second check on the same root must not throw.
            StorageSentinel.EnsureStorageReady(root, "Development");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DevelopmentDoesNotOverrideExistingSentinel()
    {
        var root = NewRoot();
        try
        {
            // An existing mismatched sentinel is fatal even for Development — auto-create
            // only happens when the sentinel is MISSING. A Production-mounted volume must
            // never be silently rewritten by a local dev startup.
            File.WriteAllText(Path.Combine(root, ".nordicbees-storage"), "Production");

            Assert.Throws<StorageSentinelException>(() => StorageSentinel.EnsureStorageReady(root, "Development"));

            // And the sentinel content must be untouched.
            Assert.Equal("Production", File.ReadAllText(Path.Combine(root, ".nordicbees-storage")).Trim());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
#endif
