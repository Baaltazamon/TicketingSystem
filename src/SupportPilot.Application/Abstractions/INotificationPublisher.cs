using SupportPilot.Application.Notifications;

namespace SupportPilot.Application.Abstractions;

public interface INotificationPublisher
{
    Task PublishAsync(NotificationMessage notification, CancellationToken cancellationToken = default);
}
