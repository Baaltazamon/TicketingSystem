using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Reads and updates user notification inbox state.
/// </summary>
public interface INotificationInboxStore
{
    /// <summary>
    /// Lists recent personal and global notifications for a user.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent notifications ordered from newest to oldest.</returns>
    Task<IReadOnlyList<Notification>> ListRecentForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a personal notification that belongs to a user.
    /// </summary>
    /// <param name="notificationId">Notification identifier.</param>
    /// <param name="userId">Owner user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification when it exists and belongs to the user.</returns>
    Task<Notification?> GetPersonalNotificationAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists notification inbox changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of affected records.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
