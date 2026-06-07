namespace SupportPilot.Application.Common;

/// <summary>
/// Lightweight authorization context used by ticket use cases.
/// </summary>
/// <param name="Id">Current user identifier.</param>
/// <param name="IsAdmin">Whether the current user has the Admin role.</param>
/// <param name="IsSupportStaff">Whether the current user is allowed to perform support staff actions.</param>
public sealed record TicketActor(Guid Id, bool IsAdmin, bool IsSupportStaff)
{
    /// <summary>Gets whether this actor can see internal ticket notes.</summary>
    public bool CanSeeInternalNotes => IsSupportStaff;
}
