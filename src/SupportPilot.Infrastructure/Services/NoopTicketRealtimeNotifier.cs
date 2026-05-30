using SupportPilot.Application.Abstractions;

namespace SupportPilot.Infrastructure.Services;

public sealed class NoopTicketRealtimeNotifier : ITicketRealtimeNotifier
{
    public Task TicketCreatedAsync(Guid ticketId, string number, string title, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task TicketUpdatedAsync(Guid ticketId, object payload, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task TicketAssignedAsync(Guid ticketId, Guid? assignedToId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task CommentAddedAsync(Guid ticketId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task AttachmentUploadedAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task SlaUpdatedAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
