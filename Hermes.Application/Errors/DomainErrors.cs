using FluentResults;

namespace Hermes.Application.Errors;

/// <summary>
/// Base class for all domain and application errors represented as FluentResults error objects.
/// </summary>
public abstract class DomainError : Error
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainError"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The human-readable message describing the error.</param>
    protected DomainError(string message) : base(message)
    {
    }
}

/// <summary>
/// Error representing a collision when an email address is already registered in the system.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class DuplicateEmailError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateEmailError"/> class.
    /// </summary>
    /// <param name="email">The duplicate email address.</param>
    public DuplicateEmailError(string email) 
        : base($"A user with email '{email}' already exists.")
    {
    }
}

/// <summary>
/// Error representing an authentication failure when the provided current password does not match the stored hash.
/// Maps to HTTP 400 Bad Request with custom Problem Type (https://hermes.dev/problems/wrong-current).
/// </summary>
public sealed class InvalidCurrentPasswordError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidCurrentPasswordError"/> class.
    /// </summary>
    public InvalidCurrentPasswordError() 
        : base("Current password verification failed.")
    {
    }
}

/// <summary>
/// Error representing a failure to locate a user by identifier, username, or email.
/// Maps to HTTP 404 Not Found.
/// </summary>
public sealed class UserNotFoundError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserNotFoundError"/> class for an identifier.
    /// </summary>
    /// <param name="userId">The missing user identifier.</param>
    public UserNotFoundError(int userId) 
        : base($"User with id '{userId}' not found.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserNotFoundError"/> class for a username or email.
    /// </summary>
    /// <param name="identifier">The missing user name or email.</param>
    /// <param name="isEmail">Whether the identifier is an email address.</param>
    public UserNotFoundError(string identifier, bool isEmail = false) 
        : base(isEmail ? $"User with email '{identifier}' not found." : $"User with name '{identifier}' not found.")
    {
    }
}

/// <summary>
/// Error representing invalid login credentials (wrong username or password).
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public sealed class InvalidCredentialsError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidCredentialsError"/> class.
    /// </summary>
    public InvalidCredentialsError() 
        : base("Invalid login or password.")
    {
    }
}

/// <summary>
/// Error representing a revoked or compromised refresh token replay attack.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public sealed class TokenCompromisedError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenCompromisedError"/> class.
    /// </summary>
    /// <param name="message">The security revocation message.</param>
    public TokenCompromisedError(string message) 
        : base(message)
    {
    }
}

/// <summary>
/// Error representing a business or input validation failure.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class ValidationError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="message">The validation failure message.</param>
    public ValidationError(string message) 
        : base(message)
    {
    }
}

/// <summary>
/// Error representing an invalid, expired, or missing two-factor email verification code.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public sealed class VerificationCodeMismatchError : DomainError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerificationCodeMismatchError"/> class.
    /// </summary>
    /// <param name="message">Optional custom message describing the verification mismatch.</param>
    public VerificationCodeMismatchError(string message = "Verification code does not match, has expired, or is missing.") 
        : base(message)
    {
    }
}

