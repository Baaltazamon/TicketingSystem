using SupportPilot.Domain;

namespace SupportPilot.Application.Notifications;

public sealed record NotificationMessage(
    Guid? UserId,
    Guid? TicketId,
    NotificationType Type,
    string Message);
