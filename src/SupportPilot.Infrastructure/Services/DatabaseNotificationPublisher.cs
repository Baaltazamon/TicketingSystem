using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Notifications;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class DatabaseNotificationPublisher(SupportPilotDbContext db) : INotificationPublisher
{
    public async Task PublishAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(new Notification
        {
            UserId = notification.UserId,
            TicketId = notification.TicketId,
            Type = notification.Type,
            Message = notification.Message
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
