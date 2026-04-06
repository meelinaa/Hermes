using Hermes.Domain.Entities;

namespace Hermes.Application.ApiService.Interface
{
    public interface IApiService
    {
        Task RegisterUserAsync(User user, CancellationToken cancellationToken);
    }
}
