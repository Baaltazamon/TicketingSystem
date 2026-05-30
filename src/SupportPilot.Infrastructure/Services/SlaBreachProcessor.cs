using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Application.Notifications;
using SupportPilot.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class SlaBreachProcessor(
    SupportPilotDbContext db,
    ITicketRealtimeNotifier realtimeNotifier,
    INotificationPublisher notificationPublisher)
{
    public async Task<int> CheckSlaAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var breachCount = 0;
        var notifications = new List<NotificationMessage>();

        var tickets = await db.Tickets
            .Where(x => x.Status != TicketStatus.Resolved && x.Status != TicketStatus.Closed && x.Status != TicketStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            var breached = false;

            if (!ticket.FirstResponseBreached && ticket.FirstResponseAt == null && ticket.FirstResponseDueAt < now)
            {
                ticket.FirstResponseBreached = true;
                breached = true;
                notifications.Add(CreateSlaNotification(ticket, "Нарушен SLA первого ответа"));
            }

            if (!ticket.ResolutionBreached && ticket.ResolutionDueAt < now)
            {
                ticket.ResolutionBreached = true;
                breached = true;
                notifications.Add(CreateSlaNotification(ticket, "Нарушен SLA решения"));
            }

            if (breached)
            {
                breachCount++;
                ticket.UpdatedAt = now;
                db.AuditLogs.Add(new AuditLog
                {
                    Action = AuditAction.SlaBreached,
                    EntityName = nameof(Ticket),
                    EntityId = ticket.Id.ToString(),
                    Details = $"SLA breached for ticket {ticket.Number}"
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var notification in notifications)
            {
                await notificationPublisher.PublishAsync(notification, cancellationToken);
            }

            await realtimeNotifier.SlaUpdatedAsync(cancellationToken);
        }

        return breachCount;
    }

    private static NotificationMessage CreateSlaNotification(Ticket ticket, string message) =>
        new(
            ticket.AssignedToId,
            ticket.Id,
            NotificationType.SlaBreached,
            $"{message}: {ticket.Number} {ticket.Title}");
}
