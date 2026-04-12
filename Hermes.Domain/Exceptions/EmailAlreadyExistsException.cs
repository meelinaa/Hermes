namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when creating a user whose e-mail is already registered.
/// Map to HTTP 409 Conflict at the API boundary.
/// </summary>
public sealed class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException()
        : base("Diese E-Mail-Adresse wird bereits verwendet. Bitte verwenden Sie eine andere E-Mail-Adresse.")
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
