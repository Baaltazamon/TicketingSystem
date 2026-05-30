using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
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
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var databaseProvider = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()?.Provider ?? "Sqlite";
        services.AddDbContext<SupportPilotDbContext>(options =>
        {
            if (databaseProvider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
            {
                options.UseNpgsql(configuration.GetConnectionString("PostgreSql"));
            }
            else if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(configuration.GetConnectionString("Sqlite"));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported database provider '{databaseProvider}'.");
            }
        });

        services.AddScoped<ISupportPilotDbContext>(provider => provider.GetRequiredService<SupportPilotDbContext>());
        services.AddScoped<JwtTokenService>();
        services.AddScoped<ITokenService>(provider => provider.GetRequiredService<JwtTokenService>());
        services.AddScoped<IPasswordHasher, AspNetPasswordHasher>();
        services.AddScoped<IUserAccountStore, UserAccountStore>();
        services.AddScoped<IFileStorage>(provider =>
        {
            var options = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>() ?? new FileStorageOptions();
            return options.Provider.Equals("Minio", StringComparison.OrdinalIgnoreCase)
                ? provider.GetRequiredService<MinioFileStorage>()
                : provider.GetRequiredService<LocalFileStorage>();
        });
        services.AddScoped<LocalFileStorage>();
        services.AddScoped<MinioFileStorage>();
        services.AddScoped<SlaBreachProcessor>();
        services.AddSingleton<IMinioClient>(_ =>
        {
            var options = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>() ?? new FileStorageOptions();
            var client = new MinioClient()
                .WithEndpoint(options.Endpoint)
                .WithCredentials(options.AccessKey, options.SecretKey);

            if (options.UseSsl)
            {
                client = client.WithSSL();
            }

            return client.Build();
        });
        services.AddHostedService<SlaMonitorService>();
        services.AddInfrastructureHealthChecks(configuration);

        return services;
    }

    private static IServiceCollection AddInfrastructureHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<SupportPilotDbContext>("database", tags: ["ready"])
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"]);

        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            healthChecks.AddRedis(redisConnectionString, "redis", tags: ["ready"]);
        }

        var rabbitMqConnectionString = configuration.GetConnectionString("RabbitMQ");
        if (!string.IsNullOrWhiteSpace(rabbitMqConnectionString))
        {
            healthChecks.AddRabbitMQ(rabbitMqConnectionString, name: "rabbitmq", tags: ["ready"]);
        }

        return services;
    }
}
