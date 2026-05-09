using Hangfire;
using Hermes.Application.Jobs;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Hermes.Api.Hangfire;

/// <summary>
/// Enqueues <see cref="NotificationJobs.SendVerificationMailAsync"/> via shared Hangfire MySQL storage (processed by Hermes.Worker).
/// </summary>
public sealed class HangfireVerificationMailJobTrigger(JobStorage jobStorage)
    : IVerificationMailJobTrigger
{
    /// <summary>Enqueues a background verification mail job for the given user and returns the Hangfire job id.</summary>
    public string? EnqueueSendVerificationMail(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");

        BackgroundJobClient client = new(jobStorage);
        string? jobId = client.Enqueue<NotificationJobs>(notificationJobs =>
            notificationJobs.SendVerificationMailAsync(userId, CancellationToken.None));
        return jobId;
    }
}
