using Hermes.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Hermes.IntegrationTests.Infrastructure;

internal static class HermesDatabaseMigrator
{
    public static async Task MigrateAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseMySql(connectionString, HermesMySqlServerVersions.PinnedMysql84)
            .Options;

        await using HermesDbContext db = new(options);
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
