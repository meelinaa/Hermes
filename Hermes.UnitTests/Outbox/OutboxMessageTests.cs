using Hermes.Domain.Entities;
using Xunit;

namespace Hermes.UnitTests.Outbox;

public sealed class OutboxMessageTests
{
    [Fact]
    public void Create_Should_Initialize_NewMessage_Correctly()
    {
        // Arrange
        string type = "UserRegisteredEvent";
        string content = "{\"UserId\":1,\"Email\":\"test@example.com\"}";
        DateTime now = DateTime.UtcNow;

        // Act
        OutboxMessage message = OutboxMessage.Create(type, content, now);

        // Assert
        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(type, message.Type);
        Assert.Equal(content, message.Content);
        Assert.Equal(now, message.CreatedAtUtc);
        Assert.Null(message.ProcessedAtUtc);
        Assert.Null(message.Error);
        Assert.Equal(0, message.RetryCount);
    }

    [Fact]
    public void MarkProcessed_Should_SetProcessedTimestamp_And_ClearError()
    {
        // Arrange
        OutboxMessage message = OutboxMessage.Create("TestEvent", "{}", DateTime.UtcNow);
        message.MarkFailed("Transient error");
        DateTime processedAt = DateTime.UtcNow.AddSeconds(5);

        // Act
        message.MarkProcessed(processedAt);

        // Assert
        Assert.Equal(processedAt, message.ProcessedAtUtc);
        Assert.Null(message.Error);
    }

    [Fact]
    public void MarkFailed_Should_IncrementRetryCount_And_SetError()
    {
        // Arrange
        OutboxMessage message = OutboxMessage.Create("TestEvent", "{}", DateTime.UtcNow);

        // Act
        message.MarkFailed("Timeout occurred");

        // Assert
        Assert.Equal(1, message.RetryCount);
        Assert.Equal("Timeout occurred", message.Error);
        Assert.Null(message.ProcessedAtUtc);

        // Act 2nd failure
        message.MarkFailed("Second failure");

        // Assert
        Assert.Equal(2, message.RetryCount);
        Assert.Equal("Second failure", message.Error);
    }
}
