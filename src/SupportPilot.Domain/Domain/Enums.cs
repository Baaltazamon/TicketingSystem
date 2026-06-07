namespace SupportPilot.Domain;

/// <summary>
/// Ticket workflow status.
/// </summary>
public enum TicketStatus
{
    /// <summary>Ticket was created and has not been taken into support work yet.</summary>
    New = 0,

    /// <summary>Support staff is actively working on the ticket.</summary>
    InProgress = 1,

    /// <summary>Support staff is waiting for the customer to provide more information.</summary>
    WaitingForCustomer = 2,

    /// <summary>The reported issue has been resolved but the ticket can still be reopened or closed.</summary>
    Resolved = 3,

    /// <summary>The ticket is completed and no further work is expected.</summary>
    Closed = 4,

    /// <summary>The ticket was cancelled before completion.</summary>
    Cancelled = 5
}

/// <summary>
/// Business priority used for sorting, escalation, and SLA policy selection.
/// </summary>
public enum TicketPriority
{
    /// <summary>Low urgency issue with the longest SLA windows.</summary>
    Low = 0,

    /// <summary>Default priority for ordinary support requests.</summary>
    Normal = 1,

    /// <summary>Important request with shortened SLA windows.</summary>
    High = 2,

    /// <summary>Critical incident with the strictest SLA windows.</summary>
    Critical = 3
}

/// <summary>
/// Type of notification stored for a user or published through the notification worker.
/// </summary>
public enum NotificationType
{
    /// <summary>A ticket was assigned to a support user.</summary>
    TicketAssigned = 0,

    /// <summary>A ticket was changed, for example its status was updated.</summary>
    TicketUpdated = 1,

    /// <summary>SLA deadline is approaching.</summary>
    SlaWarning = 2,

    /// <summary>SLA deadline has been breached.</summary>
    SlaBreached = 3,

    /// <summary>A public comment or internal note was added.</summary>
    CommentAdded = 4
}

/// <summary>
/// Auditable action type stored in the audit log.
/// </summary>
public enum AuditAction
{
    /// <summary>An entity was created.</summary>
    Created = 0,

    /// <summary>An entity was updated.</summary>
    Updated = 1,

    /// <summary>An entity was deleted.</summary>
    Deleted = 2,

    /// <summary>A ticket status was changed.</summary>
    StatusChanged = 3,

    /// <summary>A ticket assignee was changed.</summary>
    Assigned = 4,

    /// <summary>A comment or internal note was added.</summary>
    Commented = 5,

    /// <summary>A file was attached to a ticket.</summary>
    AttachmentUploaded = 6,

    /// <summary>A ticket breached one of its SLA deadlines.</summary>
    SlaBreached = 7
}
