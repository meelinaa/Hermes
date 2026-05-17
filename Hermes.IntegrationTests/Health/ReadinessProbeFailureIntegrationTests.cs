using System.Net;
using Hermes.IntegrationTests.Infrastructure;
using Testcontainers.MySql;

namespace Hermes.IntegrationTests.Health;

/// <summary>Stopping MySQL in an isolated fixture (never the shared collection) so readiness reflects DB loss.</summary>
[Trait("Integration", "Docker")]
public sealed class ReadinessProbeFailureIntegrationTests : IAsyncLifetime
{
    private MySqlContainer? _mysql;

    public async Task InitializeAsync()
    {
        _mysql = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithCleanUp(true)
            .Build();

        await _mysql.StartAsync().ConfigureAwait(false);
        await HermesDatabaseMigrator.MigrateAsync(_mysql.GetConnectionString()).ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        if (_mysql is not null)
            await _mysql.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Get_health_ready_returns_ServiceUnavailable_after_mysql_container_stops()
    {
        Assert.NotNull(_mysql);

        await using HermesApiWebApplicationFactory factory = new(_mysql!.GetConnectionString());
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage healthyResponse = await client.GetAsync(new Uri("/health/ready", UriKind.Relative)))
            Assert.Equal(HttpStatusCode.OK, healthyResponse.StatusCode);

        await _mysql.DisposeAsync();
        _mysql = null;

        HttpStatusCode? lastCode = null;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150));
            HttpResponseMessage probe = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            lastCode = probe.StatusCode;
            if (lastCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError)
                return;

            probe.Dispose();
        }

        Assert.True(
            lastCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError,
            $"Expected a failing readiness status after MySQL stopped; last HTTP status was {lastCode}.");
    }
}
