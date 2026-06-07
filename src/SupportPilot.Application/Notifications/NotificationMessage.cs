using SupportPilot.Domain;

namespace SupportPilot.Application.Notifications;

/// <summary>
/// Transport-neutral notification command published by application use cases.
/// </summary>
/// <param name="UserId">Recipient user identifier. Null means a global notification.</param>
/// <param name="TicketId">Related ticket identifier, if the notification belongs to a ticket.</param>
/// <param name="Type">Notification type.</param>
/// <param name="Message">Human-readable notification text.</param>
public sealed record NotificationMessage(
    Guid? UserId,
    Guid? TicketId,
    NotificationType Type,
    string Message);
