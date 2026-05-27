namespace SupportPilot.Application.Abstractions;

public interface IFileStorage
{
    Task<StoredFile> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken);

    string GetFullPath(string storageKey);
}

public sealed record StoredFile(string StorageKey, long SizeBytes);
