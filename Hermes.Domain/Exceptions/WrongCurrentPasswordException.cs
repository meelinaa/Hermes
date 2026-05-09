namespace Hermes.Domain.Exceptions;

/// <summary>Thrown when a password change is attempted with an incorrect current password. Map to HTTP 400 with <see cref="HermesProblemTypes.WRONG_CURRENT_PASSWORD"/> at the API boundary.</summary>
public sealed class WrongCurrentPasswordException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public WrongCurrentPasswordException()
        : base("Das eingegebene aktuelle Passwort ist nicht korrekt.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public WrongCurrentPasswordException(string message)
        : base(message)
    {
    }
}
