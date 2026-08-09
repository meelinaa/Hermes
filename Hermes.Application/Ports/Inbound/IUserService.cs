using Hermes.Application.DTOs.User;

namespace Hermes.Application.Ports.Inbound;

public interface IUserService
{
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
}
