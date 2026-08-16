namespace Hermes.Domain.Exceptions;

/// <summary>User row missing â†’ map to HTTP 404 at the API boundary.</summary>
public sealed class UserNotFoundException : DomainException
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
