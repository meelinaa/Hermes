using Hermes.Application.Ports.Inbound;
using Hermes.Domain.Events;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.EventDispatching;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.EventDispatching;

/// <summary>
/// Contains unit tests for <see cref="DomainEventDispatcher"/>,
/// verifying domain event resolution and execution via registered <see cref="IDomainEventHandler{TEvent}"/> handlers.
/// </summary>
public sealed class DomainEventDispatcherTests
{
    /// <summary>
    /// Tests that <see cref="DomainEventDispatcher.DispatchAsync"/> resolves the appropriate handler and invokes HandleAsync.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_Should_InvokeRegisteredHandler()
    {
        // Arrange
        Mock<IDomainEventHandler<UserRegisteredEvent>> handler = new();
        handler.Setup(h => h.HandleAsync(It.IsAny<UserRegisteredEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        ServiceCollection services = new();
        services.AddSingleton(handler.Object);
        ServiceProvider serviceProvider = services.BuildServiceProvider();

        UserRegisteredEvent domainEvent = new(new UserId(1), "test@example.org");
        using CancellationTokenSource cts = new();

        DomainEventDispatcher sut = new(serviceProvider);

        // Act
        await sut.DispatchAsync(domainEvent, cts.Token);

        // Assert
        handler.Verify(h => h.HandleAsync(domainEvent, cts.Token), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="DomainEventDispatcher.DispatchAsync"/> throws an <see cref="ArgumentNullException"/> when domain event is null.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_Should_ThrowArgumentNullException_WhenDomainEventIsNull()
    {
        // Arrange
        ServiceCollection services = new();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        DomainEventDispatcher sut = new(serviceProvider);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.DispatchAsync(null!));
    }
}
