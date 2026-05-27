using Microsoft.EntityFrameworkCore;
using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

public interface ISupportPilotDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<TicketCategory> TicketCategories { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<TicketComment> TicketComments { get; }
    DbSet<TicketAttachment> TicketAttachments { get; }
    DbSet<TicketStatusHistory> TicketStatusHistory { get; }
    DbSet<SlaPolicy> SlaPolicies { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
