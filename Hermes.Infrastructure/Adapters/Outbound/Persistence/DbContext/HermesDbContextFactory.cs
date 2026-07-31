using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;

/// <summary>
/// Design-time factory for <see cref="HermesDbContext"/> so <c>dotnet ef</c> can create migrations without DI / Program.cs.
/// </summary>
public sealed class HermesDbContextFactory : IDesignTimeDbContextFactory<HermesDbContext>
{
    /// <summary>Creates a configured <see cref="HermesDbContext"/> for design-time tooling.</summary>
    public HermesDbContext CreateDbContext(string[] args)
    {
        string connectionString = ResolveConnectionString();

        ServerVersion serverVersion = HermesMySqlServerVersionConstants.PinnedMysql84;

        DbContextOptionsBuilder<HermesDbContext> optionsBuilder = new();
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new HermesDbContext(optionsBuilder.Options);
    }

    /// <summary>Resolves the design-time connection string from environment variables or appsettings files.</summary>
    private static string ResolveConnectionString()
    {
        string? fromEnv = Environment.GetEnvironmentVariable("HERMES_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        foreach (string path in EnumerateAppsettingsPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out JsonElement cs)
                && cs.TryGetProperty("DefaultConnection", out JsonElement el))
            {
                string? connectionString = el.GetString();
                if (!string.IsNullOrWhiteSpace(connectionString))
                {
                    return connectionString;
                }
            }
        }

        throw new InvalidOperationException(
            "No database connection string for migrations. Set HERMES_CONNECTION_STRING or ConnectionStrings__DefaultConnection, " +
            "or add ConnectionStrings:DefaultConnection to Hermes/appsettings.json.");
    }

    /// <summary>Enumerates likely appsettings file locations for design-time connection string discovery.</summary>
    private static IEnumerable<string> EnumerateAppsettingsPaths()
    {
        string cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "appsettings.json");
        yield return Path.Combine(cwd, "Hermes", "appsettings.json");

        DirectoryInfo? dir = new(cwd);
        while (dir?.Parent != null)
        {
            dir = dir.Parent;
            yield return Path.Combine(dir.FullName, "appsettings.json");
            yield return Path.Combine(dir.FullName, "Hermes", "appsettings.json");
        }
    }
}
