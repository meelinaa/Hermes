using System.Net;
using System.Text.Json;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.IntegrationTests.Health;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class HealthProbeIntegrationTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task Get_health_live_returns_OK_without_running_database_checks()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live/extra")]
    [InlineData("/health/ready/stale")]
    [InlineData("/health/does-not-exist")]
    public async Task Get_unknown_health_route_returns_NotFound(string path)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

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

    [Fact]
    public async Task Get_health_ready_must_include_at_least_one_check_entry()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int count = json.RootElement.GetProperty("Checks").GetArrayLength();

        Assert.True(count > 0, "Expected at least one health check entry (database probe should be registered).");
    }

    [Fact]
    public async Task Get_health_ready_must_use_application_json_content_type()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
    }

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
        foreach (JsonElement entry in checks.EnumerateArray())
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

        Assert.True(foundHealthyDatabaseEntry, "Expected a 'database' check with Status 'Healthy' in the readiness JSON.");
    }

    [Fact]
    public async Task Scoped_db_context_can_connect_to_mysql_using_application_registration()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        bool canConnect = await db.Database.CanConnectAsync();

        Assert.True(canConnect);
    }
}
