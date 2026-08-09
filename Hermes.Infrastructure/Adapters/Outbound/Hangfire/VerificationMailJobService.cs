using Hangfire;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.NotificationLogs;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Infrastructure adapter implementation for enqueuing verification email background jobs into Hangfire.
/// </summary>
public sealed class VerificationMailJobService(JobStorage jobStorage)
    : IVerificationMailJobService
{
    /// <inheritdoc />
    public string? EnqueueSendVerificationMail(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");

        BackgroundJobClient client = new(jobStorage);
        string? jobId = client.Enqueue<NotificationJobService>(notificationJobs =>
            notificationJobs.SendVerificationMailAsync(userId, CancellationToken.None));
        return jobId;
    }
}
