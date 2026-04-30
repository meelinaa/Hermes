using Hermes.Application.Models;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;

namespace Hermes.Domain.Interfaces.Services;

public interface IUserService
{
    Task<UserScope> RegisterUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>Validates password against stored BCrypt hash after resolving user by email (if input contains '@') or by name.</summary>
    Task<LoginResult> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default);
    /// <param name="currentPasswordPlain">Required when <see cref="User.PasswordHash"/> carries a new plain password: current password for verification.</param>
    Task UpdateUserAsync(User user, string? currentPasswordPlain = null, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task SendVerificationMailAsync(string email, CancellationToken cancellationToken);
}
