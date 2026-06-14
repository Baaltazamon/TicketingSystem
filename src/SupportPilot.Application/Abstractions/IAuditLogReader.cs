using SupportPilot.Contracts;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Reads audit log entries using storage-optimized queries.
/// </summary>
public interface IAuditLogReader
{
    /// <summary>
    /// Lists the most recent audit log entries.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent audit log entries.</returns>
    Task<IReadOnlyList<AuditLogResponse>> ListRecentAsync(CancellationToken cancellationToken = default);
}
