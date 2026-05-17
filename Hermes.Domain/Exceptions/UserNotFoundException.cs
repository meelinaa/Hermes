namespace Hermes.Domain.Exceptions;

/// <summary>User row missing → map to HTTP 404 at the API boundary.</summary>
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
