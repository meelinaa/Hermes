using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

public interface IUserRepository
{
    /// <summary>
    /// Inserts a newly created user into the database and persists the changes.
    /// Ensures that duplicate email addresses are rejected and assigns a database-generated ID.
    /// </summary>
    ValueTask SetUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified username.
    /// Used to locate users by display name or check username existence without exposing sensitive credentials.
    /// </summary>
    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified email address.
    /// Used during registration and account lookup to verify if an email address is already registered.
    /// </summary>
    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified unique user ID.
    /// Used for authorized profile lookups and state synchronization.
    /// </summary>
    ValueTask<UserScopeDto?> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete user domain entity by username including hashed credentials for authentication verification.
    /// Used exclusively during login workflows to evaluate BCrypt password hashes.
    /// </summary>
    ValueTask<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete user domain entity by email address including hashed credentials for authentication verification.
    /// Used exclusively during login workflows to evaluate BCrypt password hashes.
    /// </summary>
    ValueTask<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the complete user domain entity by ID including hashed credentials for authentication and token validation.
    /// </summary>
    ValueTask<User?> GetUserEntityForAuthenticationByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the user entity by ID for profile updates or state modifications.
    /// </summary>
    ValueTask<User?> GetUserEntityByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updated user profile fields (such as name, email, or password hash) to the database.
    /// </summary>
    ValueTask UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user account and triggers cascading removal of associated subscriptions and tokens.
    /// </summary>
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an email verification challenge code and its expiration timestamp on the user entity.
    /// </summary>
    ValueTask SetUserEmailVerificationChallengeAsync(UserId userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the user's email address as verified after successfully validating the challenge code.
    /// </summary>
    ValueTask CompleteUserEmailVerificationAsync(UserId userId, CancellationToken cancellationToken = default);
}
