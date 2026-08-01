using Hermes.Application.DTOs.User;

namespace Hermes.Application.Ports.Inbound;

public interface IUserService
{
    Task DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
}
