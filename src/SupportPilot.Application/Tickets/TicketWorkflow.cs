using SupportPilot.Application.Common;
using SupportPilot.Domain;

namespace SupportPilot.Application.Tickets;

internal static class TicketWorkflow
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> SupportTransitions =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.New] = [TicketStatus.InProgress, TicketStatus.WaitingForCustomer, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.InProgress] = [TicketStatus.WaitingForCustomer, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.WaitingForCustomer] = [TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
            [TicketStatus.Closed] = [],
            [TicketStatus.Cancelled] = []
        };

    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> CustomerTransitions =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.New] = [TicketStatus.Cancelled],
            [TicketStatus.InProgress] = [],
            [TicketStatus.WaitingForCustomer] = [TicketStatus.InProgress],
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
            [TicketStatus.Closed] = [],
            [TicketStatus.Cancelled] = []
        };

    public static bool CanTransition(TicketStatus from, TicketStatus to, TicketActor actor) =>
        from == to || GetAllowedTransitions(from, actor).Contains(to);

    public static IReadOnlyCollection<TicketStatus> GetAllowedTransitions(TicketStatus from, TicketActor actor) =>
        actor.IsSupportStaff ? SupportTransitions[from] : CustomerTransitions[from];
}
