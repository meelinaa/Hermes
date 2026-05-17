namespace Hermes.Application.Scheduling;

public interface IVerificationMailJobTrigger
{
    string? EnqueueSendVerificationMail(int userId);
}
