using Hermes.Application.DTOs.User;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface IUserService
{
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    ValueTask<UserScopeDto?> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default);
}
