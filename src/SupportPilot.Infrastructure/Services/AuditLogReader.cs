using Microsoft.EntityFrameworkCore;
using SupportPilot.Application.Abstractions;
using SupportPilot.Contracts;
using SupportPilot.Infrastructure.Data;

namespace SupportPilot.Infrastructure.Services;

public sealed class AuditLogReader(SupportPilotDbContext db) : IAuditLogReader
{
    public async Task<IReadOnlyList<AuditLogResponse>> ListRecentAsync(CancellationToken cancellationToken = default)
    {
        return await db.AuditLogs
            .FromSqlRaw("""
                SELECT *
                FROM "AuditLogs"
                ORDER BY "CreatedAt" DESC
                LIMIT 200
                """)
            .AsNoTracking()
            .Include(x => x.Actor)
            .Select(x => new AuditLogResponse(
                x.Id,
                x.Action,
                x.EntityName,
                x.EntityId,
                x.Details,
                x.Actor == null ? null : x.Actor.DisplayName,
                x.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
