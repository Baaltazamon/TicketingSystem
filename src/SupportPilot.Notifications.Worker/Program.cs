using SupportPilot.Infrastructure;
using SupportPilot.Infrastructure.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration, options =>
{
    options.EnableHealthChecks = false;
    options.EnableSlaMonitor = true;
    options.EnableRabbitMqNotificationWorker = true;
});

var host = builder.Build();

await DatabaseInitializer.InitializeAsync(host.Services);
await host.RunAsync();
