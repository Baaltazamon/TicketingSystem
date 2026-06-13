namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Provides application-level caching without exposing a concrete cache backend to use cases or endpoints.
/// </summary>
public interface IApplicationCache
{
    /// <summary>
    /// Returns a cached value or creates and stores it when the cache entry is missing.
    /// </summary>
    /// <typeparam name="T">The value type to cache.</typeparam>
    /// <param name="group">The logical cache group used for coarse-grained invalidation.</param>
    /// <param name="key">The key that identifies the value within the group.</param>
    /// <param name="expiration">The absolute expiration relative to now.</param>
    /// <param name="factory">Factory used to create the value on cache miss.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(
        string group,
        string key,
        TimeSpan expiration,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates all future lookups for a logical cache group.
    /// </summary>
    /// <param name="group">The logical cache group to invalidate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task InvalidateGroupAsync(string group, CancellationToken cancellationToken);
}

/// <summary>
/// Contains logical cache group names used by the application.
/// </summary>
public static class CacheGroups
{
    /// <summary>
    /// Cache group for public knowledge base responses.
    /// </summary>
    public const string KnowledgeBase = "knowledge-base";

    /// <summary>
    /// Cache group for operational reporting responses.
    /// </summary>
    public const string Reports = "reports";
}
