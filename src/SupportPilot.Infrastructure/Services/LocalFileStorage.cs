using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Infrastructure.Services;

public sealed class LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<StoredFile> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        var safeFileName = Path.GetFileName(fileName);
        var storageKey = Path.Combine(ticketId.ToString("N"), $"{Guid.NewGuid():N}_{safeFileName}");
        var fullPath = GetFullPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var stream = File.Create(fullPath);
        await content.CopyToAsync(stream, cancellationToken);
        return new StoredFile(storageKey.Replace('\\', '/'), stream.Length);
    }

    public Task<StoredFileDownload?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<StoredFileDownload?>(null);
        }

        var stream = File.OpenRead(fullPath);
        return Task.FromResult<StoredFileDownload?>(new StoredFileDownload(stream, null, stream.Length));
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        var root = GetRootPath();
        Directory.CreateDirectory(root);
        return Task.FromResult(Directory.Exists(root));
    }

    private string GetFullPath(string storageKey) => Path.GetFullPath(Path.Combine(GetRootPath(), storageKey));

    private string GetRootPath()
    {
        return Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(environment.ContentRootPath, _options.RootPath);
    }
}
