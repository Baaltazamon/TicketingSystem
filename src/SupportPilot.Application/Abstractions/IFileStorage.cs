namespace SupportPilot.Application.Abstractions;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken);

    Task<StoredFileDownload?> OpenReadAsync(string storageKey, CancellationToken cancellationToken);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken);
}

public sealed record StoredFile(string StorageKey, long SizeBytes);

public sealed record StoredFileDownload(Stream Content, string? ContentType, long? SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
