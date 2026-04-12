namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when creating a user whose e-mail is already registered.
/// Map to HTTP 409 Conflict at the API boundary.
/// </summary>
public sealed class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException()
        : base("This email address is already registered. Please use a different email address.")
    {
    }

    public EmailAlreadyExistsException(string message)
        : base(message)
    {
    }

    public EmailAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
