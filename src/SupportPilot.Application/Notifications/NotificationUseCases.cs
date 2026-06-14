using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Common;
using SupportPilot.Domain;

namespace SupportPilot.Application.Notifications;

/// <summary>
/// Provides notification inbox use cases for authenticated users.
/// </summary>
public sealed class NotificationUseCases(INotificationInboxStore inboxStore)
{
    /// <summary>
    /// Lists recent personal and global notifications for the current user.
    /// </summary>
    /// <param name="userId">Current user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent notifications ordered from newest to oldest.</returns>
    public async Task<IReadOnlyList<Notification>> ListInboxAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await inboxStore.ListRecentForUserAsync(userId, cancellationToken);
    }

    /// <summary>
    /// Marks a personal notification as read.
    /// </summary>
    /// <param name="notificationId">Notification identifier.</param>
    /// <param name="userId">Current user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success, or not found when the notification does not belong to the user.</returns>
    public async Task<ApplicationResult> MarkAsReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var notification = await inboxStore.GetPersonalNotificationAsync(notificationId, userId, cancellationToken);
        if (notification is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound, "Notification not found.");
        }

        notification.IsRead = true;
        await inboxStore.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }
}
