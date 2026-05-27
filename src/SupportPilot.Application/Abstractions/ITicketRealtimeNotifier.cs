namespace SupportPilot.Application.Abstractions;

public interface ITicketRealtimeNotifier
{
    Task SlaUpdatedAsync(CancellationToken cancellationToken);
}
