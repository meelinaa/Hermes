namespace Hermes.Application.Ports.Inbound;

using Hermes.Domain.ValueObjects;

public interface INewsletterDigestService
{
    Task SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default);
}
