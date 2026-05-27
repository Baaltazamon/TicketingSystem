using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportPilot.Application.Abstractions;
using SupportPilot.Infrastructure.Auth;
using SupportPilot.Infrastructure.Data;
using SupportPilot.Infrastructure.Services;

namespace SupportPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));

        services.AddDbContext<SupportPilotDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ISupportPilotDbContext>(provider => provider.GetRequiredService<SupportPilotDbContext>());
        services.AddScoped<JwtTokenService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddHostedService<SlaMonitorService>();

        return services;
    }
}
