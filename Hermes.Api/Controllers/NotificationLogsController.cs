using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Api.Validation;
using Hermes.Application.DTOs.NotificationLogs;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hermes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/users/{userId:int}/notification-logs")]
public class NotificationLogsController(INotificationLogService notificationLogService) : ControllerBase
{
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
        [FromServices] IValidator<CreateNotificationLogRequestDto> validator,
        CancellationToken cancellationToken)
    {
        ValidationResult fv = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        NotificationLog entity = request.ToEntity(userId);
        await notificationLogService.SetNotificationLogAsync(entity, cancellationToken).ConfigureAwait(false);
        return Ok(entity.ToResponse());
    }
}
