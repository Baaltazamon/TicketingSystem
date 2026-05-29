using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SupportPilot.Infrastructure.Data;

public sealed class SupportPilotDbContextFactory : IDesignTimeDbContextFactory<SupportPilotDbContext>
{
    public SupportPilotDbContext CreateDbContext(string[] args)
    {
        var provider = GetArgument(args, "Database:Provider")
            ?? Environment.GetEnvironmentVariable("Database__Provider")
            ?? "Sqlite";

        var optionsBuilder = new DbContextOptionsBuilder<SupportPilotDbContext>();

        if (provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = GetArgument(args, "ConnectionStrings:PostgreSql")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql")
                ?? "Host=localhost;Port=5432;Database=supportpilot;Username=supportpilot;Password=supportpilot";

            optionsBuilder.UseNpgsql(connectionString);
            return new SupportPilotDbContext(optionsBuilder.Options);
        }

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = GetArgument(args, "ConnectionStrings:Sqlite")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__Sqlite")
                ?? "Data Source=supportpilot.db";

            optionsBuilder.UseSqlite(connectionString);
            return new SupportPilotDbContext(optionsBuilder.Options);
        }

        throw new InvalidOperationException($"Unsupported database provider '{provider}'.");
    }

    private static string? GetArgument(string[] args, string name)
    {
        var prefix = $"--{name}=";
        var inlineValue = args.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (inlineValue is not null)
        {
            return inlineValue[prefix.Length..];
        }

        var keyIndex = Array.FindIndex(args, argument => argument.Equals($"--{name}", StringComparison.OrdinalIgnoreCase));
        return keyIndex >= 0 && keyIndex + 1 < args.Length ? args[keyIndex + 1] : null;
    }
}
