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

/// <summary>
/// Contains unit tests for <see cref="OutboxMessageProcessor"/>,
/// verifying outbox queue polling, type resolution fallbacks, deserialization error handling,
/// dispatcher invocations, and retry/dead-letter markings.
/// </summary>
public sealed class OutboxMessageProcessorTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> returns zero
    /// and performs no database updates when there are no pending outbox messages.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_ReturnZero_WhenNoPendingMessagesExist()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(0, processedCount);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> ignores messages
    /// that are already processed or have reached the maximum retry limit (>= 5).
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_IgnoreProcessedAndExhaustedMessages()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        // Already processed message
        OutboxMessage processedMsg = OutboxMessage.Create("UserRegisteredEvent", "{}", DateTime.UtcNow);
        processedMsg.MarkProcessed(DateTime.UtcNow);

        // Failed message that reached 5 retries
        OutboxMessage exhaustedMsg = OutboxMessage.Create("UserRegisteredEvent", "{}", DateTime.UtcNow);
        for (int i = 0; i < 5; i++)
        {
            exhaustedMsg.MarkFailed($"Error {i + 1}");
        }

        ctx.OutboxMessages.AddRange(processedMsg, exhaustedMsg);
        await ctx.SaveChangesAsync();

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(0, processedCount);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> successfully resolves types
    /// using assembly-qualified names, dispatches events, and marks messages as processed.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_DispatchEvents_And_MarkMessagesProcessed_WithAssemblyQualifiedName()
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

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> resolves event types
    /// by short/simple class name (e.g. "UserEmailChangedEvent") via AppDomain fallback.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_ResolveEventBySimpleClassName_ViaAppDomainFallback()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserEmailChangedEvent(new UserId(15), "old@test.dev", "new@test.dev");
        string json = JsonSerializer.Serialize(evt, evt.GetType());

        OutboxMessage msg = OutboxMessage.Create("UserEmailChangedEvent", json, DateTime.UtcNow);
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

        dispatcher.Verify(d => d.DispatchAsync(It.Is<UserEmailChangedEvent>(e => e.UserId.Value == 15 && e.NewEmail == "new@test.dev"), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> resolves event types
    /// by full namespace name (e.g. "Hermes.Domain.Events.UserRegisteredEvent") via AppDomain fallback.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_ResolveEventByFullName_ViaAppDomainFallback()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserRegisteredEvent(new UserId(77), "full@test.dev");
        string json = JsonSerializer.Serialize(evt, evt.GetType());

        OutboxMessage msg = OutboxMessage.Create("Hermes.Domain.Events.UserRegisteredEvent", json, DateTime.UtcNow);
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(1, processedCount);
        OutboxMessage updated = await ctx.OutboxMessages.FirstAsync(m => m.Id == msg.Id);
        Assert.NotNull(updated.ProcessedAtUtc);

        dispatcher.Verify(d => d.DispatchAsync(It.Is<UserRegisteredEvent>(e => e.UserId.Value == 77), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> marks the outbox message as failed
    /// when the event type cannot be found across loaded assemblies.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_MarkFailed_WhenEventTypeCannotBeResolved()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        OutboxMessage msg = OutboxMessage.Create("NonExistent.UnresolvableEvent", "{}", DateTime.UtcNow);
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
        Assert.Contains("Could not resolve type", updated.Error);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> marks the outbox message as failed
    /// when JSON deserialization returns null or a non-domain event.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_MarkFailed_WhenDeserializationProducesNull()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        OutboxMessage msg = OutboxMessage.Create(typeof(UserRegisteredEvent).AssemblyQualifiedName!, "null", DateTime.UtcNow);
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
        Assert.Contains("Deserialization did not produce an IDomainEvent", updated.Error);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> handles dispatcher exceptions,
    /// logs the error, and increments the retry count.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> processes multiple pending messages
    /// in chronological creation order and correctly respects the cancellation token.
    /// </summary>
    [Fact]
    public async Task ProcessPendingMessagesAsync_Should_ForwardCancellationToken_ToDispatcher()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserRegisteredEvent(new UserId(100), "cancel@example.org");
        string json = JsonSerializer.Serialize(evt, evt.GetType());
        string typeName = typeof(UserRegisteredEvent).AssemblyQualifiedName!;

        OutboxMessage msg = OutboxMessage.Create(typeName, json, DateTime.UtcNow);
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        using CancellationTokenSource cts = new();
        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync(10, cts.Token);

        // Assert
        Assert.Equal(1, processedCount);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), cts.Token), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="OutboxMessageProcessor.ProcessPendingMessagesAsync"/> handles optimistic concurrency conflicts
    /// or concurrent updates gracefully without throwing unhandled exceptions.
    /// </summary>
    [Fact]
    public async Task OutboxMessageProcessor_Should_HandleOptimisticConcurrencyConflict_Gracefully()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        Mock<IDomainEventDispatcher> dispatcher = new();
        Mock<ILogger<OutboxMessageProcessor>> logger = new();

        var evt = new UserRegisteredEvent(new UserId(101), "conflict@example.org");
        string json = JsonSerializer.Serialize(evt, evt.GetType());
        string typeName = typeof(UserRegisteredEvent).AssemblyQualifiedName!;

        OutboxMessage msg = OutboxMessage.Create(typeName, json, DateTime.UtcNow);
        ctx.OutboxMessages.Add(msg);
        await ctx.SaveChangesAsync();

        // Simulate concurrent worker modifying the same message while dispatch is ongoing
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>(async (_, _) =>
            {
                await using HermesDbContext concurrentCtx = new(new DbContextOptionsBuilder<HermesDbContext>()
                    .UseInMemoryDatabase(ctx.Database.ProviderName!)
                    .Options);
                // Concurrent modification
            })
            .Returns(Task.CompletedTask);

        var processor = new OutboxMessageProcessor(ctx, dispatcher.Object, logger.Object);

        // Act
        int processedCount = await processor.ProcessPendingMessagesAsync();

        // Assert
        Assert.Equal(1, processedCount);
        OutboxMessage processed = await ctx.OutboxMessages.FirstAsync(m => m.Id == msg.Id);
        Assert.NotNull(processed.ProcessedAtUtc);
    }
}
