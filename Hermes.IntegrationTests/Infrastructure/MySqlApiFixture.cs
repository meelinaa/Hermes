using Testcontainers.MySql;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Shared harness for integration tests that need a real MySQL server plus a running API instance.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why Docker:</strong> Pomelo + MySQL readiness checks exercise actual TCP connectivity, authentication, and SQL executed by
/// <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCoreHealthChecksBuilderAddDbContextCheckExtensions.AddDbContextCheck{TContext}"/>.
/// An in-memory SQLite provider would not validate production MySQL behaviour.
/// </para>
/// <para>
/// One container is started per fixture instance; Xunit collection fixtures reuse the same instance across all tests in that collection,
/// which keeps startup cost manageable while still isolating collections from each other.
/// </para>
/// </remarks>
public sealed class MySqlApiFixture : IAsyncLifetime
{
    private MySqlContainer? _container;

    /// <summary>HTTP client factory wired to the API with valid database configuration.</summary>
    public HermesApiWebApplicationFactory Factory { get; private set; } = null!;

    /// <summary>Raw ADO.NET-style connection string produced by Testcontainers (includes credentials and database name).</summary>
    public string ConnectionString => _container?.GetConnectionString()
        ?? throw new InvalidOperationException("Fixture not initialized; Ensure InitializeAsync completed.");

    public async Task InitializeAsync() // This method is called by xUnit before any tests run. It starts the MySQL container, applies migrations, and initializes the API factory.
    {
        _container = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithCleanUp(true)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        await HermesDatabaseMigrator.MigrateAsync(_container.GetConnectionString()).ConfigureAwait(false);

        Factory = new HermesApiWebApplicationFactory(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }
}
