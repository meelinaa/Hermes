namespace Hermes.Application.Ports.Inbound;

public interface INewsletterDigestService
{
    Task SendAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default);
}
