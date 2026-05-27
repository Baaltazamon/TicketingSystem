using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SupportPilot.Api.Realtime;

[Authorize]
public sealed class TicketHub : Hub;
