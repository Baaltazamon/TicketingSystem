namespace SupportPilot.Application.Common;

public sealed record TicketActor(Guid Id, bool IsAdmin, bool IsSupportStaff)
{
    public bool CanSeeInternalNotes => IsSupportStaff;
}
