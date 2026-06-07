using Microsoft.AspNetCore.SignalR;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Api.Realtime;

/// <summary>
/// Sends ticket lifecycle events to connected SignalR clients.
/// </summary>
/// <param name="hubContext">SignalR hub context used to broadcast ticket updates.</param>
public sealed class SignalRTicketRealtimeNotifier(IHubContext<TicketHub> hubContext) : ITicketRealtimeNotifier
{
    /// <inheritdoc />
    public Task TicketCreatedAsync(Guid ticketId, string number, string title, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("ticketCreated", new { Id = ticketId, Number = number, Title = title }, cancellationToken);

    /// <inheritdoc />
    public Task TicketUpdatedAsync(Guid ticketId, object payload, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("ticketUpdated", payload, cancellationToken);

    /// <inheritdoc />
    public Task TicketAssignedAsync(Guid ticketId, Guid? assignedToId, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("ticketAssigned", new { Id = ticketId, AssignedToId = assignedToId }, cancellationToken);

    /// <inheritdoc />
    public Task CommentAddedAsync(Guid ticketId, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("commentAdded", new { Id = ticketId }, cancellationToken);

    /// <inheritdoc />
    public Task AttachmentUploadedAsync(Guid ticketId, Guid attachmentId, CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync(
            "attachmentUploaded",
            new { TicketId = ticketId, AttachmentId = attachmentId },
            cancellationToken);

    /// <inheritdoc />
    public Task SlaUpdatedAsync(CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("slaUpdated", cancellationToken);
}
