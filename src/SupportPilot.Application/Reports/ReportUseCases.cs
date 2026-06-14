using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportPilot.Application.Abstractions;
using SupportPilot.Contracts;
using SupportPilot.Domain;

namespace SupportPilot.Application.Reports;

/// <summary>
/// Provides support-staff reporting and dashboard query use cases.
/// </summary>
public sealed class ReportUseCases(
    ISupportPilotDbContext db,
    IApplicationCache cache,
    IOptions<CacheOptions> cacheOptions)
{
    /// <summary>
    /// Builds the support dashboard overview snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dashboard overview.</returns>
    public async Task<DashboardOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken)
    {
        return await cache.GetOrCreateAsync(
            CacheGroups.Reports,
            "overview",
            ReportsCacheExpiration,
            BuildOverviewAsync,
            cancellationToken);
    }

    private async Task<DashboardOverviewResponse> BuildOverviewAsync(CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;
        var dueSoon = now.AddHours(4);
        var openStatuses = new[] { TicketStatus.New, TicketStatus.InProgress, TicketStatus.WaitingForCustomer };

        var dashboardTickets = await db.Tickets
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.AssignedTo)
            .Select(x => new DashboardTicketSnapshot(
                x.Id,
                x.Number,
                x.Title,
                x.Status,
                x.Priority,
                x.Category.Name,
                x.AssignedTo == null ? null : x.AssignedTo.DisplayName,
                x.UpdatedAt,
                x.FirstResponseDueAt,
                x.ResolutionDueAt,
                x.FirstResponseAt,
                x.ResolvedAt,
                x.FirstResponseBreached,
                x.ResolutionBreached))
            .ToListAsync(token);
        var byStatus = dashboardTickets
            .GroupBy(x => x.Status)
            .Select(x => new DashboardBucketResponse(x.Key.ToString(), x.Count()))
            .OrderBy(x => x.Key)
            .ToList();
        var byPriority = dashboardTickets
            .GroupBy(x => x.Priority)
            .Select(x => new DashboardBucketResponse(x.Key.ToString(), x.Count()))
            .OrderBy(x => x.Key)
            .ToList();
        var recentTickets = dashboardTickets
            .OrderByDescending(x => x.UpdatedAt)
            .Take(8)
            .Select(ToDashboardTicketResponse)
            .ToList();
        var slaBreaches = dashboardTickets
            .Where(x => x.FirstResponseBreached || x.ResolutionBreached)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(8)
            .Select(ToDashboardTicketResponse)
            .ToList();
        var overdueTickets = dashboardTickets.Count(x =>
            IsOpen(x.Status) &&
            (
                (x.FirstResponseDueAt != null && x.FirstResponseAt == null && x.FirstResponseDueAt < now) ||
                (x.ResolutionDueAt != null && x.ResolvedAt == null && x.ResolutionDueAt < now)
            ));
        var dueSoonTickets = dashboardTickets.Count(x =>
            IsOpen(x.Status) &&
            (
                (x.FirstResponseDueAt != null && x.FirstResponseAt == null && x.FirstResponseDueAt >= now && x.FirstResponseDueAt <= dueSoon) ||
                (x.ResolutionDueAt != null && x.ResolvedAt == null && x.ResolutionDueAt >= now && x.ResolutionDueAt <= dueSoon)
            ));

        return new DashboardOverviewResponse(
            now,
            dashboardTickets.Count,
            dashboardTickets.Count(x => IsOpen(x.Status)),
            dashboardTickets.Count(x => x.Status == TicketStatus.Resolved || x.Status == TicketStatus.Closed),
            dashboardTickets.Count(x => x.AssignedTo is null && IsOpen(x.Status)),
            overdueTickets,
            dueSoonTickets,
            dashboardTickets.Count(x => x.FirstResponseBreached || x.ResolutionBreached),
            dashboardTickets.Count(x => IsOpen(x.Status) && x.Priority == TicketPriority.Critical),
            dashboardTickets.Count(x => IsOpen(x.Status) && x.Priority == TicketPriority.High),
            byStatus,
            byPriority,
            recentTickets,
            slaBreaches);

        bool IsOpen(TicketStatus status) => openStatuses.Contains(status);
    }

    private static DashboardTicketResponse ToDashboardTicketResponse(DashboardTicketSnapshot ticket) =>
        new(
            ticket.Id,
            ticket.Number,
            ticket.Title,
            ticket.Status,
            ticket.Priority,
            ticket.Category,
            ticket.AssignedTo,
            ticket.UpdatedAt,
            ticket.FirstResponseDueAt,
            ticket.ResolutionDueAt,
            ticket.FirstResponseBreached,
            ticket.ResolutionBreached);

    private TimeSpan ReportsCacheExpiration =>
        TimeSpan.FromSeconds(NormalizeCacheSeconds(cacheOptions.Value.ReportsExpirationSeconds, 30));

    private static int NormalizeCacheSeconds(int value, int fallback) => value <= 0 ? fallback : value;

    private sealed record DashboardTicketSnapshot(
        Guid Id,
        string Number,
        string Title,
        TicketStatus Status,
        TicketPriority Priority,
        string Category,
        string? AssignedTo,
        DateTimeOffset UpdatedAt,
        DateTimeOffset? FirstResponseDueAt,
        DateTimeOffset? ResolutionDueAt,
        DateTimeOffset? FirstResponseAt,
        DateTimeOffset? ResolvedAt,
        bool FirstResponseBreached,
        bool ResolutionBreached);
}
