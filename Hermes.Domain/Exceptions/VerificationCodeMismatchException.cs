namespace Hermes.Domain.Exceptions;

/// <summary>Wrong/expired verification code or missing challenge → map to HTTP 400.</summary>
public sealed class VerificationCodeMismatchException : Exception
{
    public VerificationCodeMismatchException()
        : base("Der Verifizierungscode stimmt nicht überein.")
    {
    }

    public VerificationCodeMismatchException(string message)
        : base(message)
    {
    }
}
