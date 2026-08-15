using System.Text.Json;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Events;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Outbox;

public sealed class OutboxMessageProcessorTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_DispatchEvents_And_MarkMessagesProcessed()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserRegisteredEvent(new UserId(42), "alex@example.org");
        string json = JsonSerializer.Serialize(evt, evt.GetType());
        string typeName = typeof(UserRegisteredEvent).AssemblyQualifiedName!;

        OutboxMessage msg = OutboxMessage.Create(typeName, json, DateTime.UtcNow);
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(1, processedCount);
        OutboxMessage updated = await ctx.OutboxMessages.FirstAsync(m => m.Id == msg.Id);
        Assert.NotNull(updated.ProcessedAtUtc);
        Assert.Null(updated.Error);

        dispatcher.Verify(d => d.DispatchAsync(It.Is<UserRegisteredEvent>(e => e.UserId.Value == 42 && e.Email == "alex@example.org"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_HandleException_And_MarkMessageFailed()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unreachable"));

        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserRegisteredEvent(new UserId(99), "fail@example.org");
        string json = JsonSerializer.Serialize(evt, evt.GetType());
        string typeName = typeof(UserRegisteredEvent).AssemblyQualifiedName!;

        OutboxMessage msg = OutboxMessage.Create(typeName, json, DateTime.UtcNow);
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(0, processedCount);
        OutboxMessage updated = await ctx.OutboxMessages.FirstAsync(m => m.Id == msg.Id);
        Assert.Null(updated.ProcessedAtUtc);
        Assert.Equal(1, updated.RetryCount);
        Assert.Equal("SMTP unreachable", updated.Error);
    }
}
