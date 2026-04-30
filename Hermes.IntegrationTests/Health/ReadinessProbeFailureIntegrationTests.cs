using System.Net;
using Hermes.IntegrationTests.Infrastructure;
using Testcontainers.MySql;

namespace Hermes.IntegrationTests.Health;

/// <summary>
/// Negative-path integration coverage for the readiness probe when MySQL becomes unreachable while the API keeps running.
/// </summary>
/// <remarks>
/// <para>
/// We deliberately **do not** use <see cref="HermesIntegrationCollection"/> here: stopping the container would break every other test that shares that fixture.
/// Instead each instance of this class owns a private MySQL container + <see cref="HermesApiWebApplicationFactory"/> pair that can be torn down aggressively inside the fact body.
/// </para>
/// <para>
/// ASP.NET Core maps unhealthy readiness checks to HTTP <strong>503 Service Unavailable</strong>, signalling load balancers to stop routing traffic even though the process is still alive (contrasting with liveness behaviour).
/// </para>
/// </remarks>
[Trait("Integration", "Docker")] // this trait categorizes the test as an integration test that depends on Docker; it can be used to include/exclude tests in different runs
public sealed class ReadinessProbeFailureIntegrationTests : IAsyncLifetime
{
    private MySqlContainer? _mysql;

    /// <summary>
    /// Spins up an isolated MySQL instance and applies the Hermes schema before tests execute.
    /// </summary>
    public async Task InitializeAsync()
    {
        _mysql = new MySqlBuilder()
            .WithImage("mysql:8.4")
            .WithCleanUp(true)
            .Build();

        await _mysql.StartAsync().ConfigureAwait(false);
        await HermesDatabaseMigrator.MigrateAsync(_mysql.GetConnectionString()).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures Ryuk/Testcontainers cleanup runs even when a test stops the container early.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_mysql is not null)
            await _mysql.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// After MySQL stops, the readiness endpoint must <strong>not</strong> report success: load balancers should see failure (typically HTTP 503, sometimes 500 if the health middleware surfaces an exception instead of an unhealthy status).
    /// </summary>
    /// <remarks>
    /// Steps:
    /// <list type="number">
    /// <item><description>Verify <c>/health/ready</c> is healthy while the database container is running (baseline).</description></item>
    /// <item><description>Stop and dispose the Docker container so new TCP connections to MySQL fail immediately.</description></item>
    /// <item><description>Issue another readiness request; pooled connections may briefly mask failure, so we poll briefly.</description></item>
    /// </list>
    /// We accept either <see cref="HttpStatusCode.ServiceUnavailable"/> (ideal for probes) or <see cref="HttpStatusCode.InternalServerError"/> when the pipeline maps connection faults differently—as long as it is not <see cref="HttpStatusCode.OK"/>.
    /// </remarks>
    [Fact]
    public async Task Get_health_ready_returns_ServiceUnavailable_after_mysql_container_stops()
    {
        Assert.NotNull(_mysql); // sanity check to avoid null reference if the fixture setup failed

        await using var factory = new HermesApiWebApplicationFactory(_mysql.GetConnectionString()); // create a new API factory with the connection string of the isolated MySQL instance; this ensures the API's health checks target our test container, not any shared fixture
        using HttpClient client = factory.CreateClient();

        using (HttpResponseMessage healthyResponse = await client.GetAsync(new Uri("/health/ready", UriKind.Relative)))
            Assert.Equal(HttpStatusCode.OK, healthyResponse.StatusCode);

        await _mysql.DisposeAsync(); // stopping the container simulates a sudden database failure; any new connection attempts should fail immediately, causing the readiness check to report unhealthy
        _mysql = null;

        HttpStatusCode? lastCode = null;
        for (var attempt = 0; attempt < 20; attempt++)
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
            $"Expected a failing readiness status after MySQL stopped; last HTTP status was {lastCode}."); // Service Unavailable is the expected status once the readiness check detects the database is down; if we exit the loop without seeing it, the test should fail
    }
}
