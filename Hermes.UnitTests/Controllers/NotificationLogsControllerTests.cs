using Hermes.Api.Controllers.NotificationLogs;
using Hermes.Application.DTOs.NotificationLogs;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Controllers;

/// <summary>
/// Contains unit tests for <see cref="NotificationLogsController"/>,
/// verifying audit logging records creation and DTO mappings.
/// </summary>
public sealed class NotificationLogsControllerTests
{
    /// <summary>
    /// Tests that <see cref="NotificationLogsController.Post"/> maps the request to a <see cref="NotificationLog"/>,
    /// calls the log service, and returns 200 Ok with the response DTO.
    /// </summary>
    [Fact]
    public async Task Post_Should_CreateNotificationLogAndReturnOk()
    {
        // Arrange
        Mock<INotificationLogService> logService = new();
        NotificationLog? capturedEntity = null;
        logService.Setup(s => s.SetNotificationLogAsync(It.IsAny<NotificationLog>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationLog, CancellationToken>((entity, _) => capturedEntity = entity)
            .Returns(ValueTask.CompletedTask);

        NotificationLogsController sut = new(logService.Object);

        CreateNotificationLogRequestDto request = new()
        {
            NewsId = 10,
            SentAt = new DateTime(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc),
            Status = NotificationStatus.Sent,
            Channel = DeliveryChannel.Email,
            ErrorMessage = null,
            RetryCount = 0,
            NextRetryAt = null
        };

        // Act
        ActionResult<NotificationLogResponseDto> actionResult = await sut.Post(42, request, CancellationToken.None);

        // Assert
        ObjectResult created = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        NotificationLogResponseDto response = Assert.IsType<NotificationLogResponseDto>(created.Value);

        Assert.Equal(42, response.UserId);
        Assert.Equal(10, response.NewsId);
        Assert.Equal(NotificationStatus.Sent, response.Status);
        Assert.Equal(DeliveryChannel.Email, response.Channel);
        Assert.NotNull(capturedEntity);
        Assert.Equal(42, capturedEntity!.UserId.Value);
    }
}
