namespace Hermes.Application.Scheduling;

public interface IVerificationMailJobService
{
    string? EnqueueSendVerificationMail(int userId);
}
