namespace Hermes.Domain.Exceptions;

/// <summary>Profile password change rejected â†’ map to HTTP 400 with <see cref="Hermes.Domain.Constants.HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD"/>.</summary>
public sealed class WrongCurrentPasswordException : DomainException
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
