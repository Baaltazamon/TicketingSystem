using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SupportPilot.Api.Realtime;

/// <summary>
/// SignalR hub used by authenticated clients to receive live ticket updates.
/// </summary>
[Authorize]
public sealed class TicketHub : Hub;
