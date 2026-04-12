namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when a user is required but does not exist. Map to HTTP 404 at the API boundary.
/// </summary>
public sealed class UserNotFoundException : Exception
{
    public UserNotFoundException()
        : base("The requested user was not found.")
    {
    }

    public UserNotFoundException(string message)
        : base(message)
    {
    }

    public UserNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
