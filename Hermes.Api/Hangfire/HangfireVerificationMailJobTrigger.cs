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
    public string? EnqueueSendVerificationMail(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");

        var client = new BackgroundJobClient(jobStorage);
        var jobId = client.Enqueue<NotificationJobs>(j =>
            j.SendVerificationMailAsync(userId, CancellationToken.None));
        return jobId;
    }
}
