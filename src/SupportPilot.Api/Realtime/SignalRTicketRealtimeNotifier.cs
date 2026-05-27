using Microsoft.AspNetCore.SignalR;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Api.Realtime;

public sealed class SignalRTicketRealtimeNotifier(IHubContext<TicketHub> hubContext) : ITicketRealtimeNotifier
{
    public Task SlaUpdatedAsync(CancellationToken cancellationToken) =>
        hubContext.Clients.All.SendAsync("slaUpdated", cancellationToken);
}
