using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for credential lookups and security evaluation during user authentication.
/// </summary>
public interface IUserAuthStore
{
    /// <summary>
    /// Retrieves the complete user domain entity by username including hashed credentials for authentication verification.
    /// </summary>
    /// <param name="name">The username to look up.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user entity with hashed credentials, or null if not found.</returns>
    ValueTask<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete user domain entity by email address including hashed credentials for authentication verification.
    /// </summary>
    /// <param name="email">The email address to look up.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user entity with hashed credentials, or null if not found.</returns>
    ValueTask<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete user domain entity by ID including hashed credentials for authentication and token validation.
    /// </summary>
    /// <param name="id">The unique user ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user entity with hashed credentials, or null if not found.</returns>
    ValueTask<User?> GetUserEntityForAuthenticationByIdAsync(UserId id, CancellationToken cancellationToken = default);
}
