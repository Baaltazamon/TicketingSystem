namespace SupportPilot.Domain.Domain;

public enum TicketStatus
{
    New = 0,
    InProgress = 1,
    WaitingForCustomer = 2,
    Resolved = 3,
    Closed = 4,
    Cancelled = 5
}

public enum TicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

public enum NotificationType
{
    TicketAssigned = 0,
    TicketUpdated = 1,
    SlaWarning = 2,
    SlaBreached = 3,
    CommentAdded = 4
}

public enum AuditAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    StatusChanged = 3,
    Assigned = 4,
    Commented = 5,
    AttachmentUploaded = 6,
    SlaBreached = 7
}
