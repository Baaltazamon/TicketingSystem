namespace SupportPilot.Infrastructure.Services;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public string Transport { get; set; } = "Database";
    public string QueueName { get; set; } = "supportpilot.notifications";
}
