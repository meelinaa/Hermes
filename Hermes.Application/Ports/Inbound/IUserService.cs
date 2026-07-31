using Hermes.Application.DTOs.User;
using Hermes.Application.DTOs.Login;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;

namespace Hermes.Application.Ports.Inbound;

public interface IUserService
{
    Task<UserScopeDto> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);

    Task<LoginResultDto> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default);

    Task UpdateUserAsync(User user, string? currentPasswordPlain = null, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);

    Task SendVerificationMailAsync(string email, CancellationToken cancellationToken);

    Task CheckVerificationCodeAsync(int userId, int code, CancellationToken cancellationToken = default);
}
