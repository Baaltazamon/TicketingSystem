using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Application.Tickets;

namespace SupportPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<TicketUseCases>();
        return services;
    }
}
