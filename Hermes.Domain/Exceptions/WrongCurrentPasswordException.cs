namespace Hermes.Domain.Exceptions;

/// <summary>Thrown when a password change is attempted with an incorrect current password. Map to HTTP 400 with <see cref="HermesProblemTypes.WrongCurrentPassword"/> at the API boundary.</summary>
public sealed class WrongCurrentPasswordException : Exception
{
    public WrongCurrentPasswordException()
        : base("Das eingegebene aktuelle Passwort ist nicht korrekt.")
    {
    }

    public WrongCurrentPasswordException(string message)
        : base(message)
    {
    }
}
