using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hermes.Application.Options;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Scheduling;
using Hermes.Application.Security;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

public sealed class UserVerificationService(
    IUserRepository db,
    IVerificationMailJobService verificationMailJobTrigger,
    IOptions<SecurityOptions> securityOptions) : IUserVerificationService
{
    public async Task SendVerificationMailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));

        Email normalized = Email.Parse(email);
        User? user = await db.GetUserEntityForAuthenticationByEmailAsync(normalized.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new UserNotFoundException($"User with email '{normalized.Value}' was not found.");

        verificationMailJobTrigger.EnqueueSendVerificationMail(user.Id);
    }

    public async Task CheckVerificationCodeAsync(int userId, int code, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be positive.");
        if (code is < 0 or > 999_999)
            throw new ArgumentOutOfRangeException(nameof(code), "Verification code must be a six-digit value.");

        User? user = await db.GetUserEntityForAuthenticationByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new UserNotFoundException($"User with id {userId} was not found.");
        string? stored = user.TwoFactorCode;
        DateTime? expiry = user.TwoFactorExpiry;
        if (string.IsNullOrWhiteSpace(stored) || !expiry.HasValue)
            throw new VerificationCodeMismatchException();

        DateTime expiresUtc = expiry.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiry.Value, DateTimeKind.Utc)
            : expiry.Value.ToUniversalTime();

        if (DateTime.UtcNow >= expiresUtc)
            throw new VerificationCodeMismatchException();

        string provided = code.ToString("D6", CultureInfo.InvariantCulture);
        if (!VerificationCodeMatchesStored(stored.Trim(), provided))
            throw new VerificationCodeMismatchException();

        await db.CompleteUserEmailVerificationAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Hashed-at-rest path vs legacy plaintext; comparison uses fixed-time equality on UTF-8 bytes.</summary>
    private bool VerificationCodeMatchesStored(string stored, string providedSixDigit)
    {
        bool hashingEnabled = securityOptions.Value.HashEmailVerificationCodes;
        if (hashingEnabled && LooksLikeStoredVerificationCodeHash(stored))
        {
            string expectedHash = RefreshTokenHashService.Hash(providedSixDigit);
            ReadOnlySpan<byte> a = Encoding.UTF8.GetBytes(stored);
            ReadOnlySpan<byte> b = Encoding.UTF8.GetBytes(expectedHash);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        ReadOnlySpan<byte> plainA = Encoding.UTF8.GetBytes(stored);
        ReadOnlySpan<byte> plainB = Encoding.UTF8.GetBytes(providedSixDigit);
        return CryptographicOperations.FixedTimeEquals(plainA, plainB);
    }

    private static bool LooksLikeStoredVerificationCodeHash(string stored) =>
        stored.Length == 64 && IsUpperHex64(stored.AsSpan());

    private static bool IsUpperHex64(ReadOnlySpan<char> s)
    {
        foreach (char c in s)
        {
            if (c is (>= '0' and <= '9') or (>= 'A' and <= 'F'))
                continue;
            return false;
        }

        return true;
    }
}
