using Hermes.Api.Http;
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
    /// <b>POST</b> <c>api/v1/users/{userId}/notification-logs</c> — Body:
    /// <code>
    /// {
    ///   "id": 0,
    ///   "userId": 0,
    ///   "sentAt": "2026-03-29T13:00:00Z",
    ///   "status": "Pending",
    ///   "channel": "Email",
    ///   "errorMessage": null,
    ///   "retryCount": 0,
    ///   "nextRetryAt": null
    /// }
    /// </code>
    /// <c>status</c>: <c>Pending</c>, <c>Sent</c>, <c>Failed</c> — stored as string in DB; use string in JSON if enum-as-string is enabled, else <c>0</c>/<c>1</c>/<c>2</c>.
    /// <c>channel</c>: <c>Email</c>, <c>Telegram</c> (or <c>0</c>/<c>1</c>).
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<NotificationLog>> Post(int userId, [FromBody] NotificationLog log, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        if (log.UserId != 0 && log.UserId != userId)
            return this.BadRequestProblem("NotificationLog.UserId must match the route or be 0.");
        log.UserId = userId;

        await notificationLogService.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
        return Ok(log);
    }
}
