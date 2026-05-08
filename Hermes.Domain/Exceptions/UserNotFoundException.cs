namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when a user is required but does not exist. Map to HTTP 404 at the API boundary.
/// </summary>
public sealed class UserNotFoundException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public UserNotFoundException()
        : base("The requested user was not found.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public UserNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a custom message and inner exception.</summary>
    public UserNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
