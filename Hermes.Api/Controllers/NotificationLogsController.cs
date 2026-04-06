using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

[ApiController]
[Route("api/v1/users/{userId:int}/notification-logs")]
public class NotificationLogsController(INotificationLogService notificationLogService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NotificationLog>> Post(
        int userId,
        [FromBody] NotificationLog log,
        CancellationToken cancellationToken)
    {
        if (log.UserId != 0 && log.UserId != userId)
            return BadRequest("NotificationLog.UserId must match the route or be 0.");
        log.UserId = userId;

        await notificationLogService.SetNotificationLogAsync(log, cancellationToken).ConfigureAwait(false);
        return Ok(log);
    }
}
