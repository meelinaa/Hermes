namespace Hermes.Application.Scheduling;

/// <summary>
/// Enqueues a Hangfire job on the shared storage so <see cref="Jobs.NotificationJobs.SendVerificationMailAsync"/> runs in the worker.
/// </summary>
public interface IVerificationMailJobTrigger
{
    /// <summary>Queues verification e-mail work for <paramref name="userId"/>.</summary>
    /// <returns>Hangfire job id, or <c>null</c> if enqueue failed and was swallowed by the implementation.</returns>
    string? EnqueueSendVerificationMail(int userId);
}
