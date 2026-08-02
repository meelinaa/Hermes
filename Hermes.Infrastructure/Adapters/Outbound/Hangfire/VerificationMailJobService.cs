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
    /// <summary>
    /// Enqueues a Hangfire background job to send a verification email to the specified user asynchronously.
    /// This decouples the email sending process from the API request lifecycle.
    /// </summary>
    /// <param name="userId">The ID of the user requiring verification.</param>
    /// <returns>The generated Hangfire background job identifier.</returns>
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
