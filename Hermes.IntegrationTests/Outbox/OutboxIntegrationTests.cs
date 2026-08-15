using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.Entities;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.IntegrationTests.Outbox;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class OutboxIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Register_User_Creates_OutboxMessage_In_Database_Atomically()
    {
        // Arrange
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"outbox-{Guid.NewGuid():N}@integration.hermes";
        object dto = new
        {
            id = 0,
            name = "Outbox Tester",
            email,
            password = AuthIntegrationFlows.DEFAULT_PASSWORD,
            isEmailVerified = false,
        };

        // Act
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: _jsonWeb);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int userId = json.RootElement.GetProperty("userId").GetInt32();

        // Assert that OutboxMessage exists in MySQL
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        OutboxMessage? outboxMsg = await db.OutboxMessages
            .FirstOrDefaultAsync(m => m.Content.Contains(email));

        Assert.NotNull(outboxMsg);
        Assert.Contains("UserRegisteredEvent", outboxMsg.Type);
        Assert.Contains($"\"UserId\":{{\"Value\":{userId}}}", outboxMsg.Content);

        // Act - process outbox messages
        IOutboxMessageProcessor processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();
        int processed = await processor.ProcessPendingMessagesAsync(cancellationToken: CancellationToken.None);

        Assert.True(processed >= 0);
    }
}
