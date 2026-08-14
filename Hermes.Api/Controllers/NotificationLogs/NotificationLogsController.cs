using FluentValidation;
using FluentValidation.Results;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using Hermes.Api.Constants;
using Hermes.Api.Extensions;
using Hermes.Api.Http;
using Hermes.Api.Mapping.NotificationLogs;
using Hermes.Api.Validators.NotificationLogs;
using Hermes.Application.DTOs.NotificationLogs;
using Hermes.Application.Ports.Inbound;
using Hermes.Domain.Entities;

namespace Hermes.Api.Controllers.NotificationLogs;

/// <summary>
/// Manages audit trails of dispatched notifications (e.g. newsletter emails). 
/// Enables debugging of delivery failures and ensures the duplicate-prevention window functions correctly.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users/{userId:int}/notification-logs")]
public class NotificationLogsController(INotificationLogService notificationLogService) : ControllerBase
{
    /// <summary>
    /// Records a new delivery attempt. Used by background workers to register a sent, pending, or failed dispatch,
    /// providing an audit history and enabling rate limiting (e.g. duplicate digest prevention).
    /// </summary>
    /// <remarks>
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
    /// <c>status</c>: Pending | Sent | Failed; <c>channel</c>: Email | Telegram.
    /// </remarks>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost]
    public async Task<ActionResult<NotificationLogResponseDto>> Post(
        int userId,
        [FromBody] CreateNotificationLogRequestDto request,
        CancellationToken cancellationToken)
    {

        NotificationLog entity = request.ToEntity(userId);
        await notificationLogService.SetNotificationLogAsync(entity, cancellationToken).ConfigureAwait(false);
        return Ok(entity.ToResponse());
    }
}
