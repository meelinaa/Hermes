using System.Security.Cryptography;
using System.Text;

namespace Hermes.Application.Security;

/// <summary>SHA-256 UTF-8 → uppercase hex — never persist the client plaintext.</summary>
public static class RefreshTokenHasher
{
    public static string Hash(string plainToken)
    {
        byte[]? bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(bytes);
    }
}
