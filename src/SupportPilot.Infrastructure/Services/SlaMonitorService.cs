using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportPilot.Application.Abstractions;
using SupportPilot.Domain;
using SupportPilot.Domain.Domain;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class SlaMonitorService(
    IServiceScopeFactory scopeFactory,
    ITicketRealtimeNotifier realtimeNotifier,
    ILogger<SlaMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckSlaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitor failed.");
            }
        }
    }

    private async Task CheckSlaAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupportPilotDbContext>();
        var now = DateTimeOffset.UtcNow;

        var tickets = await db.Tickets
            .Where(x => x.Status != TicketStatus.Resolved && x.Status != TicketStatus.Closed && x.Status != TicketStatus.Cancelled)
            .ToListAsync(cancellationToken);

        foreach (var ticket in tickets)
        {
            var breached = false;

            if (!ticket.FirstResponseBreached && ticket.FirstResponseAt is null && ticket.FirstResponseDueAt < now)
            {
                ticket.FirstResponseBreached = true;
                breached = true;
                db.Notifications.Add(CreateSlaNotification(ticket, "Нарушен SLA первого ответа"));
            }

            if (!ticket.ResolutionBreached && ticket.ResolutionDueAt < now)
            {
                ticket.ResolutionBreached = true;
                breached = true;
                db.Notifications.Add(CreateSlaNotification(ticket, "Нарушен SLA решения"));
            }

            if (breached)
            {
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
            await realtimeNotifier.SlaUpdatedAsync(cancellationToken);
        }
    }

    private static Notification CreateSlaNotification(Ticket ticket, string message) => new()
    {
        UserId = ticket.AssignedToId,
        TicketId = ticket.Id,
        Type = NotificationType.SlaBreached,
        Message = $"{message}: {ticket.Number} {ticket.Title}"
    };
}
