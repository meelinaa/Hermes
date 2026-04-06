using Hermes.Application.ApiService.Interface;
using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.DBContext;

namespace Hermes.Application.ApiService
{
    public class ApiService(IHermesDbContext hermesDbContext) : IApiService
    {
        public Task RegisterUserAsync(User user, CancellationToken cancellationToken)
        {
            hermesDbContext
            return Task.CompletedTask;
        }
    }
}
