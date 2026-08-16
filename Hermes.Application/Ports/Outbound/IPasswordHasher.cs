namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for cryptographic password hashing and verification,
/// decoupling the application core from specific hashing libraries and algorithms (e.g. BCrypt, Argon2).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// Computes a cryptographically secure hash of the provided plain text password.
    /// </summary>
    /// <param name="password">The plain text password to hash.</param>
    /// <returns>A salted and hashed password string.</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verifies whether the provided plain text password matches the stored cryptographic hash.
    /// </summary>
    /// <param name="password">The plain text password to verify.</param>
    /// <param name="passwordHash">The stored hash to compare against.</param>
    /// <returns>True if the password matches the hash; otherwise false.</returns>
    bool VerifyPassword(string password, string passwordHash);
}
