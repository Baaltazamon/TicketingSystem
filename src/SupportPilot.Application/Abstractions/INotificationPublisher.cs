using SupportPilot.Application.Notifications;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port used by use cases to publish notification messages without knowing the delivery transport.
/// </summary>
public interface INotificationPublisher
{
    /// <summary>
    /// Publishes a notification using the configured transport.
    /// </summary>
    /// <param name="notification">Notification command to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync(NotificationMessage notification, CancellationToken cancellationToken = default);
}
