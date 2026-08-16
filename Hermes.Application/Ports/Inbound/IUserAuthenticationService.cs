using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;

using FluentResults;

namespace Hermes.Application.Ports.Inbound;

public interface IUserAuthenticationService
{
    Task<Result<UserScopeDto>> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);

    Task<Result<LoginResultDto>> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default);

    Task<Result> UpdateUserAsync(int userId, string name, string email, string? newPasswordPlain, string? currentPasswordPlain = null, CancellationToken cancellationToken = default);
}
