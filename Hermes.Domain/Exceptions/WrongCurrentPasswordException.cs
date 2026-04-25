namespace Hermes.Domain.Exceptions;

/// <summary>Thrown when a password change is attempted with an incorrect current password. Map to HTTP 401 at the API boundary.</summary>
public sealed class WrongCurrentPasswordException : Exception
{
    public WrongCurrentPasswordException()
        : base("The current password is incorrect.")
    {
    }

    public WrongCurrentPasswordException(string message)
        : base(message)
    {
    }
}
