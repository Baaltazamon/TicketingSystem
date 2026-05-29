using Microsoft.Extensions.Diagnostics.HealthChecks;
using SupportPilot.Application.Abstractions;

namespace SupportPilot.Infrastructure.Services;

public sealed class ObjectStorageHealthCheck(IFileStorage fileStorage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var available = await fileStorage.IsAvailableAsync(cancellationToken);
        return available
            ? HealthCheckResult.Healthy("Object storage is available.")
            : HealthCheckResult.Unhealthy("Object storage is not available.");
    }
}
