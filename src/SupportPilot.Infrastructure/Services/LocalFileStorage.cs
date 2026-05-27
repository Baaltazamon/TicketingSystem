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

    public string GetFullPath(string storageKey)
    {
        var root = Path.IsPathRooted(_options.RootPath)
            ? _options.RootPath
            : Path.Combine(environment.ContentRootPath, _options.RootPath);
        return Path.GetFullPath(Path.Combine(root, storageKey));
    }
}
