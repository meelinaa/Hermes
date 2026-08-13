namespace Hermes.Domain.Exceptions;

/// <summary>Registering a duplicate e-mail â†’ map to HTTP 409 at the API boundary.</summary>
public sealed class EmailAlreadyExistsException : DomainException
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
