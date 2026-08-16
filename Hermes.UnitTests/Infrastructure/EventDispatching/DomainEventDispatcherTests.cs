using Hermes.Domain.Events;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.EventDispatching;
using MediatR;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.EventDispatching;

/// <summary>
/// Contains unit tests for <see cref="DomainEventDispatcher"/>,
/// verifying event propagation through the MediatR publisher.
/// </summary>
public sealed class DomainEventDispatcherTests
{
    /// <summary>
    /// Tests that <see cref="DomainEventDispatcher.DispatchAsync"/> forwards the domain event
    /// and cancellation token to <see cref="IPublisher.Publish"/>.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_Should_ForwardDomainEvent_ToPublisher()
    {
        // Arrange
        Mock<IPublisher> publisher = new();
        IDomainEvent domainEvent = new UserRegisteredEvent(new UserId(1), "test@example.org");
        using CancellationTokenSource cts = new();

        DomainEventDispatcher sut = new(publisher.Object);

        // Act
        await sut.DispatchAsync(domainEvent, cts.Token);

        // Assert
        publisher.Verify(p => p.Publish(domainEvent, cts.Token), Times.Once);
    }
}
