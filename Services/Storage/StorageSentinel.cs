namespace NordicBeesERP.Services.Storage;

/// <summary>
/// Fatal startup failure: the storage volume is missing, mismatched, or not writable.
/// Propagates to top level — the process must refuse to start (see Docs/infra/FILE-STORAGE-STANDARD.md §3).
/// </summary>
public class StorageSentinelException : Exception
{
    public StorageSentinelException(string message)
        : base(message)
    {
    }

    public StorageSentinelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Startup check that the storage volume is mounted, marked with the expected environment sentinel, and writable.
/// Any failure throws <see cref="StorageSentinelException"/> — never logs, never exits, never swallows.
/// </summary>
public static class StorageSentinel
{
    private const string SentinelFileName = ".nordicbees-storage";

    public static void EnsureStorageReady(string root, string environment)
    {
        var sentinelPath = Path.Combine(root, SentinelFileName);

        if (File.Exists(sentinelPath))
        {
            var actual = File.ReadAllText(sentinelPath).Trim();
            if (!string.Equals(actual, environment, StringComparison.Ordinal))
                throw new StorageSentinelException(
                    $"Kritinė klaida: žymeklio failo „{sentinelPath}“ turinys „{actual}“ neatitinka laukiamos aplinkos „{environment}“. " +
                    "Patikrinkite, ar konteineris paleistas su teisingu failų tomuku.");
        }
        else if (environment == "Development")
        {
            // Documented Development exception: auto-create the sentinel so local dev works without a host mount.
            Directory.CreateDirectory(root);
            File.WriteAllText(sentinelPath, environment);
        }
        else
        {
            throw new StorageSentinelException(
                $"Kritinė klaida: nerastas žymeklio failas „{sentinelPath}“. " +
                "Failų tomas /var/lib/nordicbees nėra prijungtas prie konteinerio — paleidimas atšauktas.");
        }

        // Root must be writable: write and delete a temp file, even if the write/flush fails.
        var checkPath = Path.Combine(root, $".write-check-{Guid.NewGuid():N}");
        try
        {
            using (var fs = new FileStream(checkPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(new byte[] { 1, 2, 3 });
                fs.Flush(flushToDisk: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            throw new StorageSentinelException(
                $"Kritinė klaida: katalogas „{root}“ nerašomas ({ex.Message}).", ex);
        }
        finally
        {
            if (File.Exists(checkPath))
                File.Delete(checkPath);
        }
    }
}
