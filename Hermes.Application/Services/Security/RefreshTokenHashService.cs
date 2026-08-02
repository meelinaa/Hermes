using System.Security.Cryptography;
using System.Text;

namespace Hermes.Application.Services.Security;

/// <summary>
/// Provides SHA-256 hashing for refresh token plain-text values.
/// SHA-256 UTF-8 → uppercase hex — never persist the client plaintext.
/// </summary>
public static class RefreshTokenHashService
{
    /// <summary>
    /// Hashes a plain-text refresh token using SHA-256 and returns the result as an uppercase hex string.
    /// </summary>
    /// <param name="plainToken">The plain-text refresh token to hash.</param>
    /// <returns>The uppercase hex-encoded SHA-256 hash.</returns>
    public static string Hash(string plainToken)
    {
        byte[]? bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes);
    }
}
