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
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseMySql(connectionString, HermesMySqlServerVersions.PinnedMysql84)
            .Options;

        await using HermesDbContext db = new(options); // creates a new instance of the DbContext with the specified options; this is a standalone context used only for applying migrations, not shared with the API's DI container
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false); // Applies any pending migrations to the database; if the database is already up to date, this is a no-op. If migrations are missing or fail, this will throw an exception, which would indicate a test setup issue that should be fixed before running tests.
    }
}
