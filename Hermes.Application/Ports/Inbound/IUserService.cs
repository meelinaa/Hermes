using FluentResults;
using Hermes.Application.DTOs.User;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Inbound;

public interface IUserService
{
    ValueTask<Result> DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    ValueTask<Result<UserScopeDto>> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    ValueTask<Result<UserScopeDto>> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    ValueTask<Result<UserScopeDto>> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default);
}
