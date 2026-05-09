namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when the e-mail verification code is wrong, expired, or no challenge exists. Map to HTTP 400 at the API boundary.
/// </summary>
public sealed class VerificationCodeMismatchException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public VerificationCodeMismatchException()
        : base("Der Verifizierungscode stimmt nicht überein.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public VerificationCodeMismatchException(string message)
        : base(message)
    {
    }
}
