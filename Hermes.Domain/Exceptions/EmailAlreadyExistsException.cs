namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when creating a user whose e-mail is already registered.
/// Map to HTTP 409 Conflict at the API boundary.
/// </summary>
public sealed class EmailAlreadyExistsException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public EmailAlreadyExistsException()
        : base("This email address is already registered. Please use a different email address.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public EmailAlreadyExistsException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a custom message and inner exception.</summary>
    public EmailAlreadyExistsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
