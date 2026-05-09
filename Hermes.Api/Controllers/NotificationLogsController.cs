using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Api.Validation;
using Hermes.Application.Models.NotificationLogs;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>Notification logs under <c>api/v1/users/{userId}/notification-logs</c>.</summary>
[Authorize]
[ApiController]
[Route("api/v1/users/{userId:int}/notification-logs")]
public class NotificationLogsController(INotificationLogService notificationLogService) : ControllerBase
{
    /// <summary>Append a notification log entry.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users/{userId}/notification-logs</c> — Body omits <c>userId</c> (route + bearer scope the row).
    /// <code>
    /// {
    ///   "newsId": null,
    ///   "sentAt": "2026-03-29T13:00:00Z",
    ///   "status": "Pending",
    ///   "channel": "Email",
    ///   "errorMessage": null,
    ///   "retryCount": 0,
    ///   "nextRetryAt": null
    /// }
    /// </code>
    /// <c>status</c>: <c>Pending</c>, <c>Sent</c>, <c>Failed</c> — stored as string in DB.
    /// <c>channel</c>: <c>Email</c>, <c>Telegram</c>.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<NotificationLogResponse>> Post(
        int userId,
        [FromBody] CreateNotificationLogRequest request,
        [FromServices] IValidator<CreateNotificationLogRequest> validator,
        CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        ValidationResult fv = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        NotificationLog entity = request.ToEntity(userId);
        await notificationLogService.SetNotificationLogAsync(entity, cancellationToken).ConfigureAwait(false);
        return Ok(entity.ToResponse());
    }
}
