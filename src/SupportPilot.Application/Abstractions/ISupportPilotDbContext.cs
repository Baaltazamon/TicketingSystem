using Microsoft.EntityFrameworkCore;
using SupportPilot.Domain;

namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application-facing database context abstraction used by use cases.
/// </summary>
public interface ISupportPilotDbContext
{
    /// <summary>User accounts.</summary>
    DbSet<User> Users { get; }

    /// <summary>Application roles.</summary>
    DbSet<Role> Roles { get; }

    /// <summary>User-role assignments.</summary>
    DbSet<UserRole> UserRoles { get; }

    /// <summary>Ticket categories.</summary>
    DbSet<TicketCategory> TicketCategories { get; }

    /// <summary>Support tickets.</summary>
    DbSet<Ticket> Tickets { get; }

    /// <summary>Ticket comments and internal notes.</summary>
    DbSet<TicketComment> TicketComments { get; }

    /// <summary>Ticket attachment metadata.</summary>
    DbSet<TicketAttachment> TicketAttachments { get; }

    /// <summary>Ticket status transition history.</summary>
    DbSet<TicketStatusHistory> TicketStatusHistory { get; }

    /// <summary>SLA policies.</summary>
    DbSet<SlaPolicy> SlaPolicies { get; }

    /// <summary>Knowledge base categories.</summary>
    DbSet<KnowledgeBaseCategory> KnowledgeBaseCategories { get; }

    /// <summary>Knowledge base articles.</summary>
    DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; }

    /// <summary>User and global notifications.</summary>
    DbSet<Notification> Notifications { get; }

    /// <summary>Audit events.</summary>
    DbSet<AuditLog> AuditLogs { get; }

    /// <summary>
    /// Persists pending changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of affected records.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
