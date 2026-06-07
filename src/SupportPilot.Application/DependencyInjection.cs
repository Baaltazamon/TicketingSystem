using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Application.Auth;
using SupportPilot.Application.Tickets;

namespace SupportPilot.Application;

/// <summary>
/// Registers application-layer services and use cases.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds application use cases to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection used by the host application.</param>
    /// <returns>The same service collection so registrations can be chained.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthUseCases>();
        services.AddScoped<TicketUseCases>();
        return services;
    }
}
