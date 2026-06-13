using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Infrastructure.Services;

/// <summary>
/// Implements application caching on top of the configured distributed cache provider.
/// </summary>
public sealed class DistributedApplicationCache(
    IDistributedCache cache,
    IOptions<CacheOptions> options) : IApplicationCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly CacheOptions options = options.Value;

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string group,
        string key,
        TimeSpan expiration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var version = await GetGroupVersionAsync(group, cancellationToken);
        var cacheKey = BuildCacheKey(group, version, key);
        var cached = await cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var value = JsonSerializer.Deserialize<T>(cached, SerializerOptions);
            if (value is not null)
            {
                return value;
            }
        }

        var created = await factory(cancellationToken);
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(created, SerializerOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            },
            cancellationToken);

        return created;
    }

    /// <inheritdoc />
    public Task InvalidateGroupAsync(string group, CancellationToken cancellationToken) =>
        cache.SetStringAsync(
            BuildVersionKey(group),
            Guid.NewGuid().ToString("N"),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(NormalizeSeconds(options.DefaultExpirationSeconds))
            },
            cancellationToken);

    private async Task<string> GetGroupVersionAsync(string group, CancellationToken cancellationToken)
    {
        var versionKey = BuildVersionKey(group);
        var version = await cache.GetStringAsync(versionKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        version = Guid.NewGuid().ToString("N");
        await cache.SetStringAsync(
            versionKey,
            version,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(NormalizeSeconds(options.DefaultExpirationSeconds))
            },
            cancellationToken);

        return version;
    }

    private static string BuildVersionKey(string group) => $"supportpilot:{group}:version";

    private static string BuildCacheKey(string group, string version, string key) => $"supportpilot:{group}:{version}:{key}";

    private static int NormalizeSeconds(int seconds) => seconds > 0 ? seconds : 300;
}
