using Testcontainers.MySql;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>MySQL + API factory; uses real TCP/SQL (not SQLite) so Pomelo and DB health checks match production.</summary>
public sealed class MySqlApiFixture : IAsyncLifetime
{
    private MySqlContainer? _container;

    public HermesApiWebApplicationFactory Factory { get; private set; } = null!;

    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Fixture not initialized; Ensure InitializeAsync completed.");

    public async Task InitializeAsync()
    {
        _container = new MySqlBuilder("mysql:8.4")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        await HermesDatabaseMigrationService.MigrateAsync(_container.GetConnectionString()).ConfigureAwait(false);

        Factory = new HermesApiWebApplicationFactory(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
