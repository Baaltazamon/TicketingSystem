using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Infrastructure.Services;

public sealed class MinioFileStorage(IMinioClient minioClient, IOptions<FileStorageOptions> options) : IFileStorage
{
    private readonly FileStorageOptions _options = options.Value;

    public async Task<StoredFile> SaveAsync(Guid ticketId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        var safeFileName = Path.GetFileName(fileName);
        var storageKey = $"{ticketId:N}/{Guid.NewGuid():N}_{safeFileName}";
        var length = content.CanSeek ? content.Length : -1;

        await minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(storageKey)
            .WithStreamData(content)
            .WithObjectSize(length)
            .WithContentType("application/octet-stream"), cancellationToken);

        return new StoredFile(storageKey, length < 0 ? 0 : length);
    }

    public async Task<StoredFileDownload?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        var memory = new MemoryStream();
        try
        {
            var stat = await minioClient.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(storageKey), cancellationToken);

            await minioClient.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_options.BucketName)
                .WithObject(storageKey)
                .WithCallbackStream(stream => stream.CopyTo(memory)), cancellationToken);
            memory.Position = 0;
            return new StoredFileDownload(memory, stat.ContentType, stat.Size);
        }
        catch
        {
            await memory.DisposeAsync();
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);
        await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(storageKey), cancellationToken);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureBucketAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        var exists = await minioClient.BucketExistsAsync(new BucketExistsArgs()
            .WithBucket(_options.BucketName), cancellationToken);

        if (!exists)
        {
            await minioClient.MakeBucketAsync(new MakeBucketArgs()
                .WithBucket(_options.BucketName), cancellationToken);
        }
    }
}
