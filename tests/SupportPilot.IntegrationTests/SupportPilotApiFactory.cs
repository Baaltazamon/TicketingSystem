using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace SupportPilot.IntegrationTests;

public sealed class SupportPilotApiFactory : WebApplicationFactory<Program>
{
    public string DatabasePath { get; } = Path.Combine(Path.GetTempPath(), $"supportpilot-tests-{Guid.NewGuid():N}.db");
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), $"supportpilot-files-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = $"Data Source={DatabasePath}",
                ["ConnectionStrings:Redis"] = "",
                ["ConnectionStrings:RabbitMQ"] = "",
                ["FileStorage:Provider"] = "Local",
                ["FileStorage:RootPath"] = StoragePath,
                ["Jwt:SeedAdminEmail"] = "admin@supportpilot.local",
                ["Jwt:SeedAdminPassword"] = "Admin123!"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }
        }
        catch (IOException)
        {
            // SQLite can keep the file handle briefly on Windows after the test host is disposed.
        }

        if (Directory.Exists(StoragePath))
        {
            Directory.Delete(StoragePath, recursive: true);
        }
    }
}
