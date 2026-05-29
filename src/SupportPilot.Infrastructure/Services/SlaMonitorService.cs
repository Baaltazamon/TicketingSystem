using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SupportPilot.Infrastructure.Services;

public sealed class SlaMonitorService(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<SlaBreachProcessor>();
                await processor.CheckSlaAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitor failed.");
            }
        }
    }
}
