using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving;

public interface IEmailReceiver
{
    Task<EmailResult> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<EmailResult>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<EmailResult>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default);
}
