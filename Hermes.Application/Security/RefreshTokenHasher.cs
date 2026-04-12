using System.Security.Cryptography;
using System.Text;

namespace Hermes.Application.Security;

/// <summary>
/// Produces a deterministic hash of the client-provided refresh token so we never store the plain secret in the database.
/// </summary>
public static class RefreshTokenHasher
{
    /// <summary>SHA-256 over UTF-8 bytes, returned as uppercase hex (64 chars).</summary>
    public static string Hash(string plainToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes);
    }
}
