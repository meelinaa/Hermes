namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when an action requires a verified e-mail address. Map to HTTP 403 at the API boundary.
/// </summary>
public sealed class EmailNotVerifiedException : Exception
{
    public EmailNotVerifiedException()
        : base("The email address has not been verified yet.")
    {
    }

    public EmailNotVerifiedException(string message)
        : base(message)
    {
    }

    public EmailNotVerifiedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
