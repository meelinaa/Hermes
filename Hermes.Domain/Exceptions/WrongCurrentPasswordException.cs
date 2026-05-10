namespace Hermes.Domain.Exceptions;

/// <summary>Profile password change rejected → map to HTTP 400 with <see cref="Hermes.Domain.HermesProblemTypes.WRONG_CURRENT_PASSWORD"/>.</summary>
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
