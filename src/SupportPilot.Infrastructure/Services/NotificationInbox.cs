using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Notifications;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class NotificationInbox(SupportPilotDbContext db) : INotificationInboxStore
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

    public async Task<IReadOnlyList<Notification>> ListRecentForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.Notifications
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Notifications"
                WHERE "UserId" = {userId} OR "UserId" IS NULL
                ORDER BY "CreatedAt" DESC
                LIMIT 100
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<Notification?> GetPersonalNotificationAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return db.Notifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == userId,
            cancellationToken);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
