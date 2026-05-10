using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class NotificationLogServiceTests
{
    [Fact]
    public async Task SetNotificationLogAsync_Should_DelegateToStore()
    {
        Mock<INotificationLogStore> db = new();
        NotificationLog log = new() { UserId = 1, NewsId = 2 };
        db.Setup(dataStore => dataStore.SetNotificationLogAsync(log, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        NotificationLogService sut = new(db.Object);
        await sut.SetNotificationLogAsync(log);

        db.Verify(dataStore => dataStore.SetNotificationLogAsync(log, It.IsAny<CancellationToken>()), Times.Once);
    }
}
