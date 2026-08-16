using Hermes.Application.Ports.Outbound;

namespace Hermes.Infrastructure.Adapters.Outbound.Security;

/// <summary>
/// Adapter implementation of <see cref="IPasswordHasher"/> using BCrypt.Net-Next
/// for secure, adaptive work-factor password hashing.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// Computes a BCrypt hash of the provided password using the default enhanced work factor.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>A formatted BCrypt hash string.</returns>
    public string HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// Verifies a plain text password against a stored BCrypt hash.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="passwordHash">The stored BCrypt hash.</param>
    /// <returns>True if the password matches the hash; otherwise false.</returns>
    public bool VerifyPassword(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
