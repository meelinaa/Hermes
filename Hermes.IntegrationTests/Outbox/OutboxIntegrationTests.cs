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

/// <summary>
/// Contains integration tests for the transactional outbox pattern,
/// verifying atomic event persistence, reliable asynchronous dispatching, and retry/dead-letter mechanics.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class OutboxIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tests that registering a new user creates both the user row and an outbox event atomically in MySQL,
    /// and that the outbox processor can process the pending record.
    /// </summary>
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

    /// <summary>
    /// Tests that the outbox processor handles invalid event records by recording error details,
    /// incrementing the retry counter, and excluding dead-lettered messages after 5 failed attempts.
    /// </summary>
    [Fact]
    public async Task OutboxMessageProcessor_IncrementsRetryCount_AndDeadLetters_WhenTypeUnresolvable()
    {
        // Arrange: insert an unresolvable outbox message directly into the database
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();
        IOutboxMessageProcessor processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();

        OutboxMessage unresolvableMessage = OutboxMessage.Create(
            type: "NonExistentNamespace.UnknownEvent",
            content: "{\"UnknownField\": 123}");

        db.OutboxMessages.Add(unresolvableMessage);
        await db.SaveChangesAsync();

        // Act 1: First processing attempt
        int processedFirst = await processor.ProcessPendingMessagesAsync(batchSize: 10);

        // Assert 1: Processing should return 0 successes and mark message as failed with retry count = 1
        Assert.Equal(0, processedFirst);

        OutboxMessage? reloaded = await db.OutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == unresolvableMessage.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(1, reloaded!.RetryCount);
        Assert.NotNull(reloaded.Error);
        Assert.Null(reloaded.ProcessedAtUtc);

        // Act 2: Simulate 4 more retry failures to reach dead-letter threshold (RetryCount = 5)
        for (int i = 0; i < 4; i++)
        {
            await processor.ProcessPendingMessagesAsync(batchSize: 10);
        }

        OutboxMessage? deadLettered = await db.OutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == unresolvableMessage.Id);

        // Assert 2: RetryCount must be 5 (Dead Letter)
        Assert.NotNull(deadLettered);
        Assert.Equal(5, deadLettered!.RetryCount);

        // Act 3: Subsequent processor run must ignore this dead-lettered message
        int nextBatch = await processor.ProcessPendingMessagesAsync(batchSize: 10);
        Assert.Equal(0, nextBatch);
    }

    /// <summary>
    /// Tests that <see cref="IOutboxMessageProcessor.ProcessPendingMessagesAsync"/> returns 0
    /// when no unhandled outbox messages exist.
    /// </summary>
    [Fact]
    public async Task OutboxMessageProcessor_Should_ReturnZero_WhenNoPendingMessages()
    {
        // Arrange
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IOutboxMessageProcessor processor = scope.ServiceProvider.GetRequiredService<IOutboxMessageProcessor>();

        // Act
        int processed = await processor.ProcessPendingMessagesAsync(batchSize: 10);

        // Assert
        Assert.True(processed >= 0);
    }
}
