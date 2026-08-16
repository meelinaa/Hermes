using Hangfire;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.NotificationLogs;
using Hermes.Domain.ValueObjects;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Infrastructure adapter implementation for enqueuing verification email background jobs into Hangfire.
/// </summary>
public sealed class VerificationMailJobWrapper(JobStorage jobStorage)
    : IVerificationMailJobService
{
    /// <inheritdoc />
    public string? EnqueueSendVerificationMail(UserId userId)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");

        BackgroundJobClient client = new(jobStorage);
        string? jobId = client.Enqueue<NotificationJobService>(notificationJobs =>
            notificationJobs.SendVerificationMailAsync(userId, CancellationToken.None));
        return jobId;
    }
}
