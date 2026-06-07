namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port for storing, reading, deleting, and health-checking ticket attachment files.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a ticket attachment stream in the configured file storage provider.
    /// </summary>
    /// <param name="ticketId">Ticket identifier used to build a storage namespace.</param>
    /// <param name="fileName">Original file name supplied by the client.</param>
    /// <param name="content">Attachment content stream.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Stored file key and persisted size.</returns>
    Task<StoredFile> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored file for download.
    /// </summary>
    /// <param name="storageKey">Provider-specific storage key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Download stream and metadata, or null when the object does not exist.</returns>
    Task<StoredFileDownload?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a stored file if it exists.
    /// </summary>
    /// <param name="storageKey">Provider-specific storage key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>
    /// Checks whether the configured storage provider is reachable.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when storage is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Metadata returned after a file has been stored.
/// </summary>
/// <param name="StorageKey">Provider-specific storage key.</param>
/// <param name="SizeBytes">Stored file size in bytes.</param>
public sealed record StoredFile(string StorageKey, long SizeBytes);

/// <summary>
/// Open file stream returned for attachment download.
/// </summary>
/// <param name="Content">Readable file content stream.</param>
/// <param name="ContentType">MIME content type, if known.</param>
/// <param name="SizeBytes">Stored file size in bytes, if known.</param>
public sealed record StoredFileDownload(Stream Content, string? ContentType, long? SizeBytes) : IAsyncDisposable
{
    /// <summary>Disposes the wrapped content stream.</summary>
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
