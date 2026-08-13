using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for managing two-factor email verification challenges and validating verification OTP codes.
/// </summary>
public sealed class UserVerificationService(
    IUserRepository db,
    IVerificationMailJobService verificationMailJobTrigger,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider) : IUserVerificationService
{
    /// <summary>
    /// Enqueues a background job to send a verification email with a numeric challenge code to the specified address.
    /// </summary>
    /// <param name="email">The user email address to verify.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="email"/> is null or whitespace.</exception>
    /// <exception cref="UserNotFoundException">Thrown when no user matching the provided email is found.</exception>
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

    /// <summary>
    /// Validates a user-supplied 6-digit verification code against the stored challenge, ensuring code equality and non-expiration.
    /// </summary>
    /// <param name="userId">The unique identifier of the user completing verification.</param>
    /// <param name="code">The 6-digit integer verification code.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> or <paramref name="code"/> is out of valid bounds.</exception>
    /// <exception cref="UserNotFoundException">Thrown when the specified user is missing.</exception>
    /// <exception cref="VerificationCodeMismatchException">Thrown when the code does not match, has expired, or is missing.</exception>
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

        if (timeProvider.GetUtcNow().UtcDateTime >= expiresUtc)
            throw new VerificationCodeMismatchException();

        string provided = code.ToString("D6", CultureInfo.InvariantCulture);
        if (!VerificationCodeMatchesStored(stored.Trim(), provided))
            throw new VerificationCodeMismatchException();

        await db.CompleteUserEmailVerificationAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares the provided 6-digit code against the stored challenge (hashed or legacy plaintext) using fixed-time equality to prevent timing attacks.
    /// </summary>
    /// <param name="stored">The challenge code stored in the user record (either 64-char hex hash or 6-digit legacy string).</param>
    /// <param name="providedSixDigit">The formatted 6-digit input string.</param>
    /// <returns><c>true</c> if the provided code matches the stored challenge; otherwise, <c>false</c>.</returns>
    private bool VerificationCodeMatchesStored(string stored, string providedSixDigit)
    {
        bool hashingEnabled = securityOptions.Value.HashEmailVerificationCodes;
        if (hashingEnabled && LooksLikeStoredVerificationCodeHash(stored))
        {
            string expectedHash = RefreshTokenHashUtility.Hash(providedSixDigit);
            ReadOnlySpan<byte> a = Encoding.UTF8.GetBytes(stored);
            ReadOnlySpan<byte> b = Encoding.UTF8.GetBytes(expectedHash);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        ReadOnlySpan<byte> plainA = Encoding.UTF8.GetBytes(stored);
        ReadOnlySpan<byte> plainB = Encoding.UTF8.GetBytes(providedSixDigit);
        return CryptographicOperations.FixedTimeEquals(plainA, plainB);
    }

    /// <summary>
    /// Evaluates whether a stored code string matches the format of a 64-character SHA-256 uppercase hex hash.
    /// </summary>
    private static bool LooksLikeStoredVerificationCodeHash(string stored) =>
        stored.Length == 64 && IsUpperHex64(stored.AsSpan());

    /// <summary>
    /// Checks if all characters in the given span are valid uppercase hexadecimal digits ('0'-'9', 'A'-'F').
    /// </summary>
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
