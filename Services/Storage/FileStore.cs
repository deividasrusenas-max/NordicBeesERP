using System.Data;
using System.Data.Common;
using System.IO;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services.Storage;

/// <summary>
/// Root of the unified file storage volume (host-mounted at /var/lib/nordicbees/data).
/// </summary>
public class FileStorageOptions
{
    public string Root { get; set; } = "/var/lib/nordicbees/data";
}

/// <summary>
/// Content-addressed blob store backed by the `files` table. All reads intentionally ignore deleted_at:
/// soft-deleted accounting documents must stay readable/downloadable.
/// </summary>
public class FileStore : IFileStore
{
    private readonly FileStorageOptions _options;
    private readonly IDbContextFactory<NordicBeesERPContext> _dbFactory;

    public FileStore(IOptions<FileStorageOptions> options, IDbContextFactory<NordicBeesERPContext> dbFactory)
    {
        _options = options.Value;
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// Stores content (deduplicated by SHA-256), inserts a `files` row, and returns its identity.
    /// </summary>
    public async Task<StoredFile> SaveAsync(Stream content, FileMetadata meta, CancellationToken ct)
    {
        var tmpDir = Path.Combine(_options.Root, "tmp");
        Directory.CreateDirectory(tmpDir);

        var tmpPath = Path.Combine(tmpDir, $"upload-{Guid.NewGuid():N}.bin");
        try
        {
            long byteSize = 0;
            string sha256;
            using (var sha = SHA256.Create())
            {
                await using var tmp = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None);
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, ct)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    await tmp.WriteAsync(buffer.AsMemory(0, read), ct);
                    byteSize += read;
                }

                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                // fsync so an interrupted write never leaves a partial blob (sync Flush(flushToDisk) is the only fsync API; one short blocking call per upload)
                tmp.Flush(true);
                sha256 = Convert.ToHexString(sha.Hash).ToLowerInvariant();
            }

            var blobDir = Path.Combine(_options.Root, "blobs", sha256[..2], sha256[2..4]);
            var finalPath = Path.Combine(blobDir, sha256);
            if (File.Exists(finalPath) && new FileInfo(finalPath).Length == byteSize)
            {
                File.Delete(tmpPath); // identical content already on disk
            }
            else
            {
                Directory.CreateDirectory(blobDir);
                File.Move(tmpPath, finalPath, overwrite: true); // rename on the same volume is atomic — a blob either fully exists or not at all
            }

            var newId = await InsertFileRowAsync(sha256, byteSize, meta, ct);
            return new StoredFile(newId, sha256, byteSize);
        }
        catch
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { /* best-effort cleanup */ }
            }

            throw;
        }
    }

    /// <summary>
    /// Opens the stored blob for reading. The caller owns and must dispose the returned stream.
    /// Soft-deleted rows remain readable (these are accounting documents).
    /// </summary>
    public async Task<Stream> OpenAsync(long fileId, CancellationToken ct)
    {
        var sha256 = await GetSha256Async(fileId, ct);
        if (sha256 is null)
        {
            throw new MissingBlobException(fileId);
        }

        var path = BlobPath(sha256);
        if (!File.Exists(path))
        {
            throw new MissingBlobException(fileId);
        }

        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
    }

    /// <summary>
    /// True when the `files` row exists (regardless of deleted_at) and its blob is present on disk.
    /// </summary>
    public async Task<bool> ExistsAsync(long fileId, CancellationToken ct)
    {
        var sha256 = await GetSha256Async(fileId, ct);
        return sha256 is not null && File.Exists(BlobPath(sha256));
    }

    /// <summary>
    /// Marks the `files` row deleted (deleted_at + deleted_reason). Never deletes the blob from disk.
    /// A missing row is a silent no-op.
    /// </summary>
    public async Task SoftDeleteAsync(long fileId, string reason, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE files SET deleted_at = {0}, deleted_reason = {1} WHERE id = {2}",
            DateTime.UtcNow, reason, fileId);
    }

    private string BlobPath(string sha256) => Path.Combine(_options.Root, "blobs", sha256[..2], sha256[2..4], sha256);

    private async Task<string?> GetSha256Async(long fileId, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs)
        {
            await conn.OpenAsync(ct);
        }

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT sha256 FROM files WHERE id = @p0";
            cmd.Parameters.Add(CreateParameter(conn, "@p0", fileId));
            return (string?)(await cmd.ExecuteScalarAsync(ct));
        }
        finally
        {
            if (openedByUs)
            {
                await conn.CloseAsync();
            }
        }
    }

    private async Task<long> InsertFileRowAsync(string sha256, long byteSize, FileMetadata meta, CancellationToken ct)
    {
        await using var db = _dbFactory.CreateDbContext();
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs)
        {
            await conn.OpenAsync(ct);
        }

        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO files (sha256, byte_size, mime_type, original_filename, module, entity_type, entity_id, created_at, created_by) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)";
            cmd.Parameters.Add(CreateParameter(conn, "@p0", sha256));
            cmd.Parameters.Add(CreateParameter(conn, "@p1", byteSize));
            cmd.Parameters.Add(CreateParameter(conn, "@p2", meta.MimeType));
            cmd.Parameters.Add(CreateParameter(conn, "@p3", meta.OriginalFilename));
            cmd.Parameters.Add(CreateParameter(conn, "@p4", meta.Module));
            cmd.Parameters.Add(CreateParameter(conn, "@p5", meta.EntityType));
            cmd.Parameters.Add(CreateParameter(conn, "@p6", meta.EntityId));
            cmd.Parameters.Add(CreateParameter(conn, "@p7", DateTime.UtcNow));
            cmd.Parameters.Add(CreateParameter(conn, "@p8", meta.CreatedBy));
            await cmd.ExecuteNonQueryAsync(ct);

            using var idCmd = conn.CreateCommand();
            idCmd.CommandText = "SELECT LAST_INSERT_ID()";
            var newId = Convert.ToInt64(await idCmd.ExecuteScalarAsync(ct));
            return newId;
        }
        finally
        {
            if (openedByUs)
            {
                await conn.CloseAsync();
            }
        }
    }

    private static IDbDataParameter CreateParameter(DbConnection conn, string name, object? value)
    {
        var p = conn.CreateCommand().CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        return p;
    }
}
