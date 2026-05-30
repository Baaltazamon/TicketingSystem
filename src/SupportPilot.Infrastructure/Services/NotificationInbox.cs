using SupportPilot.Application.Notifications;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class NotificationInbox(SupportPilotDbContext db)
{
    public async Task StoreAsync(NotificationMessage notification, CancellationToken cancellationToken = default)
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
