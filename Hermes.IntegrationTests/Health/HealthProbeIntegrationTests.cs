using System.Net;
using System.Text.Json;
using Hermes.Infrastructure.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.IntegrationTests.Health;

/// <summary>
/// Validates Kubernetes-style health endpoints exposed by <see cref="Hermes.Api.Hosting.ApiApplicationPipelineExtensions"/>.
/// </summary>
/// <remarks>
/// These tests run against the shared MySQL-backed API fixture (see <see cref="HermesIntegrationCollection"/>).
/// They assume Docker is available because Testcontainers manages the database lifecycle.
/// Negative cases here stick to the <strong>same</strong> factory instance so we do not boot a second <see cref="HermesApiWebApplicationFactory"/>
/// in-process (Serilog’s bootstrap logger is frozen after the first host build). HTTP verbs such as POST may still yield <c>200 OK</c> on health endpoints with default ASP.NET Core mapping—that behaviour is not asserted here; instead we assert routing (404) and readiness JSON contracts (no <c>Unhealthy</c>/<c>Degraded</c> while MySQL is up).
/// </remarks>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class HealthProbeIntegrationTests(MySqlApiFixture fixture)
{
    /// <summary>
    /// Liveness must succeed whenever the ASP.NET Core process is accepting HTTP requests, without querying MySQL.
    /// </summary>
    /// <remarks>
    /// The API maps <c>/health/live</c> with <c>Predicate = _ =&gt; false</c>, which intentionally registers no checks.
    /// Orchestrators use this to restart crashed pods even when the database is temporarily unreachable.
    /// </remarks>
    [Fact]
    public async Task Get_health_live_returns_OK_without_running_database_checks()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative)); // this endpoint should always return 200 OK as long as the API process is running.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Ensures typos or extra path segments do not accidentally hit the real probe (operators might paste a wrong URL and assume it works).
    /// </summary>
    [Theory]
    [InlineData("/health/live/extra")]
    [InlineData("/health/ready/stale")]
    [InlineData("/health/does-not-exist")]
    public async Task Get_unknown_health_route_returns_NotFound(string path)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path); // we expect 404 Not Found for any path that does not exactly match the registered health endpoints.

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Negative guard: with a healthy database container the readiness aggregate must never surface <c>Unhealthy</c> or <c>Degraded</c>, otherwise Kubernetes would drain traffic incorrectly.
    /// </summary>
    [Fact]
    public async Task Get_health_ready_aggregate_Status_must_not_be_Unhealthy_or_Degraded_when_mysql_is_running()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("Status", out JsonElement aggregate));
        string? aggregateStatus = aggregate.GetString();
        Assert.NotNull(aggregateStatus);
        Assert.NotEqual("Unhealthy", aggregateStatus, StringComparer.OrdinalIgnoreCase);
        Assert.NotEqual("Degraded", aggregateStatus, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Negative guard: every reported component must be healthy—an unexpected <c>Unhealthy</c> database row means the probe wiring or migrations diverged from production expectations.
    /// </summary>
    [Fact]
    public async Task Get_health_ready_must_not_list_Unhealthy_or_Degraded_components_when_mysql_is_running()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement checks = json.RootElement.GetProperty("Checks");

        foreach (JsonElement entry in checks.EnumerateArray()) 
        {
            string? component = entry.GetProperty("Component").GetString();
            string? status = entry.GetProperty("Status").GetString();
            Assert.True(
                status is not null
                && !string.Equals(status, "Unhealthy", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(status, "Degraded", StringComparison.OrdinalIgnoreCase),
                $"Component '{component}' reported unexpected status '{status}'.");
        }
    }

    /// <summary>
    /// Negative guard: an empty <c>Checks</c> array would mean readiness ran zero probes—load balancers would get a false sense of safety while nothing validated the database.
    /// </summary>
    [Fact]
    public async Task Get_health_ready_must_include_at_least_one_check_entry()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative)); //

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int count = json.RootElement.GetProperty("Checks").GetArrayLength();

        Assert.True(count > 0, "Expected at least one health check entry (database probe should be registered).");
    }

    /// <summary>
    /// Negative guard: readiness must advertise <c>application/json</c>; plain text or HTML would break automated probes expecting structured diagnostics.
    /// </summary>
    [Fact]
    public async Task Get_health_ready_must_use_application_json_content_type()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
    }

    /// <summary>
    /// </summary>
    /// <remarks>
    /// Unlike liveness, this endpoint executes <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.DbContextHealthCheck{TContext}"/> against
    /// <see cref="HermesDbContext"/>. A <strong>200 OK</strong> response implies the server considers itself safe to receive traffic that depends on persistence.
    /// The handler writes JSON summarizing each check’s status (see <c>ApiApplicationPipelineExtensions</c>).
    /// </remarks>
    [Fact]
    public async Task Get_health_ready_returns_OK_when_mysql_is_available()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument json = await JsonDocument.ParseAsync(stream);
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("Status", out JsonElement statusElement));
        Assert.Equal("Healthy", statusElement.GetString());
    }

    /// <summary>
    /// When readiness succeeds, the JSON payload must contain each registered check with <c>Healthy</c> status—anything else indicates a regression in mapping or tagging.
    /// </summary>
    /// <remarks>
    /// This guards against an endpoint returning HTTP 200 with an empty or malformed body (clients parsing probes would mis-read availability).
    /// </remarks>
    [Fact]
    public async Task Get_health_ready_JSON_lists_database_check_as_Healthy_when_mysql_is_up()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using Stream stream = await response.Content.ReadAsStreamAsync();
        using JsonDocument json = await JsonDocument.ParseAsync(stream);
        JsonElement root = json.RootElement;

        Assert.True(root.TryGetProperty("Checks", out JsonElement checks));

        bool foundHealthyDatabaseEntry = false;
        foreach (JsonElement entry in checks.EnumerateArray()) // iterate through each check entry in the "Checks" array 
        {
            if (entry.TryGetProperty("Component", out JsonElement component)
                && component.GetString() == "database"
                && entry.TryGetProperty("Status", out JsonElement state)
                && state.GetString() == "Healthy")
            {
                foundHealthyDatabaseEntry = true;
                break;
            }
        }

        Assert.True(foundHealthyDatabaseEntry, "Expected a 'database' check with Status 'Healthy' in the readiness JSON.");    }

    /// <summary>
    /// Confirms the migrated schema is usable by resolving the same <see cref="HermesDbContext"/> instance the API registers in DI.
    /// </summary>
    /// <remarks>
    /// This is slightly broader than <c>/health/ready</c>: we explicitly open a DI scope and call
    /// <see cref="Microsoft.EntityFrameworkCore.DatabaseFacade.CanConnectAsync(System.Threading.CancellationToken)"/> to prove EF can talk to MySQL
    /// using the **production** provider stack (Pomelo). If migrations were missing, connectivity might still succeed but operations could fail later—this test pairs with migration execution in <see cref="MySqlApiFixture"/>.
    /// </remarks>
    [Fact]
    public async Task Scoped_db_context_can_connect_to_mysql_using_application_registration()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        bool canConnect = await db.Database.CanConnectAsync();

        Assert.True(canConnect);
    }
}
