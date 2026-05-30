using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Application.Auth;
using SupportPilot.Application.Tickets;

namespace SupportPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthUseCases>();
        services.AddScoped<TicketUseCases>();
        return services;
    }
}
