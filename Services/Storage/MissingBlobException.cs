namespace NordicBeesERP.Services.Storage;

/// <summary>
/// Thrown when a requested file id has no files row, or its blob is missing from disk.
/// </summary>
public class MissingBlobException : InvalidOperationException
{
    public long FileId { get; }

    public MissingBlobException(long fileId)
        : base($"Failas neegzistuoja failų saugykloje (id {fileId}).")
    {
        FileId = fileId;
    }
}
