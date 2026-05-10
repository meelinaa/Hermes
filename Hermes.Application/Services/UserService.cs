using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Hermes.Application.Models.Login;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Security;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Options;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services;

public sealed class UserService(
    IUserStore db,
    IVerificationMailJobTrigger verificationMailJobTrigger,
    IOptions<SecurityOptions> securityOptions) : IUserService
{
    /// <summary>Registers a new user, normalizes fields, hashes the plain password, and returns the created user scope.</summary>
    public async Task<UserScope> RegisterUserAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("User name is required.");
        request.Name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("User email is required.");
        Email email = Email.Parse(request.Email);
        request.Email = email.Value;
        request.Password = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "");
        User user = new()
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.Password
        };
        await db.SetUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (user.Id <= 0)
            throw new InvalidOperationException("Failed to create user.");
        UserScope userScope = new()
        {
            Name = user.Name,
            Email = user.Email,
            UserId = user.Id,
            IsEmailVerified = false
        };
        return userScope;
    }

    /// <summary>Authenticates a user by e-mail or name and verifies the supplied plain password.</summary>
    public async Task<LoginResult> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nameOrEmail))
            return new LoginResult(false, "Name or email is required.", null);
        if (string.IsNullOrEmpty(password))
            return new LoginResult(false, "Password is required.", null);

        string? key = nameOrEmail.Trim();
        User? user = key.Contains('@', StringComparison.Ordinal)
            ? await db.GetUserEntityForAuthenticationByEmailAsync(key, cancellationToken).ConfigureAwait(false)
            : await db.GetUserEntityForAuthenticationByNameAsync(key, cancellationToken).ConfigureAwait(false);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return new LoginResult(false, "Invalid login or password.", null);

        bool valid;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch
        {
            valid = false;
        }

        if (!valid)
            return new LoginResult(false, "Invalid login or password.", null);

        return new LoginResult(true, null, user.Id, user.Email, user.Name);
    }

    /// <summary>Updates user profile data and optionally changes the password after current-password verification.</summary>
    public async Task UpdateUserAsync(User user, string? currentPasswordPlain = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(user.Name))
            throw new ArgumentException("Name is required.", nameof(user));
        if (string.IsNullOrEmpty(user.Email))
            throw new ArgumentException("Email is required.", nameof(user));

        Email normalizedEmail = Email.Parse(user.Email);
        user.Email = normalizedEmail.Value;

        string? newPlain = user.PasswordHash;
        string? hashedForDb = null;
        if (!string.IsNullOrWhiteSpace(newPlain))
        {
            if (string.IsNullOrWhiteSpace(currentPasswordPlain))
                throw new ArgumentException("Current password is required when setting a new password.", nameof(currentPasswordPlain));

            User? existing = await db.GetUserEntityByIdAsync(user.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
                throw new UserNotFoundException($"User with id {user.Id} was not found.");
            if (string.IsNullOrEmpty(existing.PasswordHash))
                throw new InvalidOperationException("Cannot change password: no password is set for this account.");

            bool valid;
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(currentPasswordPlain.Trim(), existing.PasswordHash);
            }
            catch
            {
                valid = false;
            }

            if (!valid)
                throw new WrongCurrentPasswordException();

            hashedForDb = BCrypt.Net.BCrypt.HashPassword(newPlain.Trim());
        }

        user.PasswordHash = hashedForDb;
        await db.UpdateUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Deletes the specified user scope.</summary>
    public async Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a user scope by display name.</summary>
    public async Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        return await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a user scope by user identifier.</summary>
    public async Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be greater than zero.", nameof(id));
        return await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns a user scope by e-mail address.</summary>
    public async Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        return await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Enqueues a verification e-mail for the user identified by e-mail address.</summary>
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

    /// <summary>Validates and consumes a six-digit verification code for the specified user.</summary>
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

    /// <summary>
    /// Matches a user-entered six-digit string to the stored challenge. When hashing is enabled and the DB value looks like a SHA-256 hex digest, compares hashes; otherwise compares plaintext (legacy rows / hashing disabled).
    /// </summary>
    private bool VerificationCodeMatchesStored(string stored, string providedSixDigit)
    {
        bool hashingEnabled = securityOptions.Value.HashEmailVerificationCodes;
        if (hashingEnabled && LooksLikeStoredVerificationCodeHash(stored))
        {
            string expectedHash = RefreshTokenHasher.Hash(providedSixDigit);
            ReadOnlySpan<byte> a = Encoding.UTF8.GetBytes(stored);
            ReadOnlySpan<byte> b = Encoding.UTF8.GetBytes(expectedHash);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        ReadOnlySpan<byte> plainA = Encoding.UTF8.GetBytes(stored);
        ReadOnlySpan<byte> plainB = Encoding.UTF8.GetBytes(providedSixDigit);
        return CryptographicOperations.FixedTimeEquals(plainA, plainB);
    }

    /// <summary>Heuristic: persisted value from <see cref="RefreshTokenHasher"/> is 64 uppercase hex chars.</summary>
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
