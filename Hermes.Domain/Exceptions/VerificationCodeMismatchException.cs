namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when the e-mail verification code is wrong, expired, or no challenge exists. Map to HTTP 400 at the API boundary.
/// </summary>
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
