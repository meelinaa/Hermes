using Hangfire;
using Hermes.Application.Jobs;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Hermes.Api.Hangfire;

public sealed class VerificationMailJobService(JobStorage jobStorage)
    : IVerificationMailJobService
{
    /// <summary>
    /// Enqueues a Hangfire background job to send a verification email to the specified user asynchronously.
    /// This decouples the email sending process from the API request lifecycle.
    /// </summary>
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
