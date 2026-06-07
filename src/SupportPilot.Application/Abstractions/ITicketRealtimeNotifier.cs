namespace SupportPilot.Application.Abstractions;

/// <summary>
/// Application port used to broadcast live ticket updates to connected clients.
/// </summary>
public interface ITicketRealtimeNotifier
{
    /// <summary>Broadcasts that a ticket has been created.</summary>
    /// <param name="ticketId">Created ticket identifier.</param>
    /// <param name="number">Created ticket number.</param>
    /// <param name="title">Created ticket title.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TicketCreatedAsync(Guid ticketId, string number, string title, CancellationToken cancellationToken);

    /// <summary>Broadcasts that a ticket has been updated.</summary>
    /// <param name="ticketId">Updated ticket identifier.</param>
    /// <param name="payload">Transport payload sent to clients.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TicketUpdatedAsync(Guid ticketId, object payload, CancellationToken cancellationToken);

    /// <summary>Broadcasts that a ticket assignment has changed.</summary>
    /// <param name="ticketId">Ticket identifier.</param>
    /// <param name="assignedToId">Assigned support user identifier, or null when unassigned.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TicketAssignedAsync(Guid ticketId, Guid? assignedToId, CancellationToken cancellationToken);

    /// <summary>Broadcasts that a comment or internal note has been added.</summary>
    /// <param name="ticketId">Ticket identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CommentAddedAsync(Guid ticketId, CancellationToken cancellationToken);

    /// <summary>Broadcasts that an attachment has been uploaded.</summary>
    /// <param name="ticketId">Ticket identifier.</param>
    /// <param name="attachmentId">Attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AttachmentUploadedAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>Broadcasts that one or more SLA breach flags changed.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SlaUpdatedAsync(CancellationToken cancellationToken);
}
