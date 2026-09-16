using System.IO;
using System.Threading.Tasks;

namespace NordicBeesERP.Services.Storage;

/// <summary>
/// The only approved path to the unified file storage root (blobs + `files` table).
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Stores content, deduplicates by SHA-256, inserts a `files` row, returns its identity.
    /// </summary>
    Task<StoredFile> SaveAsync(Stream content, FileMetadata meta, CancellationToken ct);

    /// <summary>
    /// Opens the stored blob for reading. Throws MissingBlobException (Lithuanian message) when the blob is absent.
    /// </summary>
    Task<Stream> OpenAsync(long fileId, CancellationToken ct);

    /// <summary>
    /// True when the `files` row exists and its blob is present on disk.
    /// </summary>
    Task<bool> ExistsAsync(long fileId, CancellationToken ct);

    /// <summary>
    /// Marks the `files` row deleted (deleted_at + deleted_reason). Never deletes the blob from disk — these are accounting documents.
    /// </summary>
    Task SoftDeleteAsync(long fileId, string reason, CancellationToken ct);
}

/// <summary>
/// Identity of a stored blob (the files row).
/// </summary>
public sealed class StoredFile
{
    public long Id { get; }
    public string Sha256 { get; }
    public long ByteSize { get; }

    public StoredFile(long id, string sha256, long byteSize)
    {
        Id = id;
        Sha256 = sha256;
        ByteSize = byteSize;
    }
}

/// <summary>
/// Caller-supplied metadata stored in the files row (sha256/byte_size/id/created_at are assigned by the store).
/// </summary>
public sealed class FileMetadata
{
    public string Module { get; }
    public string EntityType { get; }
    public long? EntityId { get; }
    public string? MimeType { get; }
    public string? OriginalFilename { get; }
    public string? CreatedBy { get; }

    public FileMetadata(string module, string entityType, long? entityId, string? mimeType = null, string? originalFilename = null, string? createdBy = null)
    {
        Module = module;
        EntityType = entityType;
        EntityId = entityId;
        MimeType = mimeType;
        OriginalFilename = originalFilename;
        CreatedBy = createdBy;
    }
}
