namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when an action requires a verified e-mail address. Map to HTTP 403 at the API boundary.
/// </summary>
public sealed class EmailNotVerifiedException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public EmailNotVerifiedException()
        : base("The email address has not been verified yet.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public EmailNotVerifiedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a custom message and inner exception.</summary>
    public EmailNotVerifiedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
