namespace SupportPilot.Application.Abstractions;

public interface ITicketRealtimeNotifier
{
    Task TicketCreatedAsync(Guid ticketId, string number, string title, CancellationToken cancellationToken);

    Task TicketUpdatedAsync(Guid ticketId, object payload, CancellationToken cancellationToken);

    Task TicketAssignedAsync(Guid ticketId, Guid? assignedToId, CancellationToken cancellationToken);

    Task CommentAddedAsync(Guid ticketId, CancellationToken cancellationToken);

    Task AttachmentUploadedAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken);

    Task SlaUpdatedAsync(CancellationToken cancellationToken);
}
