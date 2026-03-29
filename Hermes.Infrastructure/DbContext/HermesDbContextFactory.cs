using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace Hermes.Infrastructure.Data;
// Example CLI Command: dotnet ef migrations add InitialCreate --project Hermes.Infrastructure --startup-project Hermes
// Example CLI Command with Connection String: dotnet ef migrations add InitialCreate --project Hermes.Infrastructure --startup-project Hermes --connection "Server=localhost;Port=3308;Database=hermes;User=root;Password=password1234!;"
// Example update command: dotnet ef database update --project Hermes.Infrastructure --startup-project Hermes
// Example update command with Connection String: dotnet ef database update --project Hermes.Infrastructure --startup-project Hermes --connection "Server=localhost;Port=3308;Database=hermes;User=root;Password=password1234!;"
// Example remove command: dotnet ef migrations remove --project Hermes.Infrastructure --startup-project Hermes
// Example remove command with Connection String: dotnet ef migrations remove --project Hermes.Infrastructure --startup-project Hermes --connection "Server=localhost;Port=3308;Database=hermes;User=root;Password=password1234!;"

/// <summary>
/// Design-time factory for <see cref="HermesDbContext"/> so <c>dotnet ef</c> can create migrations without DI / Program.cs.
/// This is used by the CLI to create migrations.
/// </summary>
public sealed class HermesDbContextFactory : IDesignTimeDbContextFactory<HermesDbContext>
{
    /// <inheritdoc />
    public HermesDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();

        // Fixed server version avoids contacting the server during design-time model build.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var optionsBuilder = new DbContextOptionsBuilder<HermesDbContext>();
        optionsBuilder.UseMySql(connectionString, serverVersion);
        return new HermesDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Resolve the connection string from the environment variables or the appsettings.json file.
    /// </summary>
    /// <returns>The connection string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no connection string is found.</exception>
    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("HERMES_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        foreach (var path in EnumerateAppsettingsPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                && cs.TryGetProperty("DefaultConnection", out var el))
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s;
                }
            }
        }

        throw new InvalidOperationException(
            "No database connection string for migrations. Set HERMES_CONNECTION_STRING or ConnectionStrings__DefaultConnection, " +
            "or add ConnectionStrings:DefaultConnection to Hermes/appsettings.json.");
    }

    /// <summary>
    /// Enumerate the appsettings.json files in the current directory and the parent directories.
    /// </summary>
    /// <returns></returns>
    private static IEnumerable<string> EnumerateAppsettingsPaths()
    {
        var cwd = Directory.GetCurrentDirectory();
        yield return Path.Combine(cwd, "appsettings.json");
        yield return Path.Combine(cwd, "Hermes", "appsettings.json");

        var dir = new DirectoryInfo(cwd);
        while (dir?.Parent != null)
        {
            dir = dir.Parent;
            yield return Path.Combine(dir.FullName, "appsettings.json");
            yield return Path.Combine(dir.FullName, "Hermes", "appsettings.json");
        }
    }
}
