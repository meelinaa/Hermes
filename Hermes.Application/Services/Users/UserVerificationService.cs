using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Hermes.Application.Errors;
using Hermes.Application.Options.Auth;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for managing two-factor email verification challenges and validating verification OTP codes.
/// Follows ISP by depending on segregated <see cref="IUserAuthStore"/> and <see cref="IUserVerificationStore"/> ports.
/// </summary>
public sealed class UserVerificationService(
    IUserAuthStore authStore,
    IUserVerificationStore verificationStore,
    IVerificationMailJobService verificationMailJobTrigger,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider) : IUserVerificationService
{
    /// <summary>
    /// Enqueues a background job to send a verification email with a numeric challenge code to the specified address.
    /// </summary>
    /// <param name="email">The user email address to verify.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="Result"/> indicating success or a domain error.</returns>
    public async Task<Result> SendVerificationMailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Fail(new ValidationError("Email cannot be null or whitespace."));

        Email normalized;
        try
        {
            normalized = Email.Parse(email);
        }
        catch (Exception ex)
        {
            return Result.Fail(new ValidationError(ex.Message));
        }

        User? user = await authStore.GetUserEntityForAuthenticationByEmailAsync(normalized.Value, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(normalized.Value, isEmail: true));

        verificationMailJobTrigger.EnqueueSendVerificationMail(user.Id);
        return Result.Ok();
    }

    /// <summary>
    /// Validates a user-supplied 6-digit verification code against the stored challenge, ensuring code equality and non-expiration.
    /// </summary>
    /// <param name="userId">The unique identifier of the user completing verification.</param>
    /// <param name="code">The 6-digit integer verification code.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="Result"/> indicating success or a domain error.</returns>
    public async Task<Result> CheckVerificationCodeAsync(UserId userId, int code, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail(new ValidationError("User id must be positive."));
        if (code is < 0 or > 999_999)
            return Result.Fail(new ValidationError("Verification code must be a six-digit value."));

        User? user = await authStore.GetUserEntityForAuthenticationByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(userId.Value));

        string? stored = user.TwoFactorCode;
        DateTime? expiry = user.TwoFactorExpiry;
        if (string.IsNullOrWhiteSpace(stored) || !expiry.HasValue)
            return Result.Fail(new VerificationCodeMismatchError());

        DateTime expiresUtc = expiry.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(expiry.Value, DateTimeKind.Utc)
            : expiry.Value.ToUniversalTime();

        if (timeProvider.GetUtcNow().UtcDateTime >= expiresUtc)
            return Result.Fail(new VerificationCodeMismatchError("Verification code has expired."));

        string provided = code.ToString("D6", CultureInfo.InvariantCulture);
        if (!VerificationCodeMatchesStored(stored.Trim(), provided))
            return Result.Fail(new VerificationCodeMismatchError());

        await verificationStore.CompleteUserEmailVerificationAsync(userId, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
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
