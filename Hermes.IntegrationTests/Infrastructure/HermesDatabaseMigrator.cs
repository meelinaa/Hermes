using Hermes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Applies EF Core migrations from <see cref="Hermes.Infrastructure"/> against the MySQL instance used by integration tests.
/// </summary>
/// <remarks>
/// The API host expects the same relational schema as production (users, news, refresh tokens, Hangfire tables created lazily by Hangfire, etc.).
/// Running <see cref="Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync"/> ensures <c>/health/ready</c> database probe and all controllers share a valid schema.
/// </remarks>
internal static class HermesDatabaseMigrator
{
    /// <summary>
    /// Builds a standalone <see cref="HermesDbContext"/> pointed at <paramref name="connectionString"/> and applies pending migrations.
    /// </summary>
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        var serverVersion = ServerVersion.AutoDetect(connectionString) // AutoDetect queries the database for version information, which is necessary for Pomelo to generate compatible SQL; it may throw if the server is unreachable or credentials are wrong, which would indicate a test setup issue
            ?? throw new InvalidOperationException("Failed to detect MySQL server version; check connection string and server availability.");
        var options = new DbContextOptionsBuilder<HermesDbContext>() // We use the same provider and version detection as the API to ensure compatibility; if this migration step fails, the API's own migrations would likely fail too, causing readiness checks to fail and tests to break in less obvious ways.
            .UseMySql(connectionString, serverVersion)
            .Options;

        await using var db = new HermesDbContext(options); // creates a new instance of the DbContext with the specified options; this is a standalone context used only for applying migrations, not shared with the API's DI container
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false); // Applies any pending migrations to the database; if the database is already up to date, this is a no-op. If migrations are missing or fail, this will throw an exception, which would indicate a test setup issue that should be fixed before running tests.
    }
}
