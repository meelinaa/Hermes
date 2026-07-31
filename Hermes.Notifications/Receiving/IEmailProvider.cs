using Hermes.Notifications.Receiving.Models;

namespace Hermes.Notifications.Receiving;

public interface IEmailProvider
{
    Task<EmailResultDto> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<EmailResultDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<EmailResultDto>> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default);
}
