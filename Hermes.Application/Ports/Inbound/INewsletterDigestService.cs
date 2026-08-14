using FluentResults;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface INewsletterDigestService
{
    Task<Result<bool>> SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default);
}
