namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Configures application response caching.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "Cache";

    /// <summary>
    /// Cache provider name. Supported values are <c>Memory</c> and <c>Redis</c>.
    /// </summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>
    /// Default cache expiration in seconds.
    /// </summary>
    public int DefaultExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Public knowledge base cache expiration in seconds.
    /// </summary>
    public int KnowledgeBaseExpirationSeconds { get; set; } = 300;

    /// <summary>
    /// Operational reports cache expiration in seconds.
    /// </summary>
    public int ReportsExpirationSeconds { get; set; } = 30;
}
