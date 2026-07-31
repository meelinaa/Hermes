using Hermes.Application.DTOs.User;
using Hermes.Application.DTOs.Login;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;

namespace Hermes.Application.Services;

public interface IUserService
{
    Task<UserScope> RegisterUserAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    Task<LoginResult> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default);

    Task UpdateUserAsync(User user, string? currentPasswordPlain = null, CancellationToken cancellationToken = default);

    Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default);

    Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);

    Task SendVerificationMailAsync(string email, CancellationToken cancellationToken);

    Task CheckVerificationCodeAsync(int userId, int code, CancellationToken cancellationToken = default);
}
