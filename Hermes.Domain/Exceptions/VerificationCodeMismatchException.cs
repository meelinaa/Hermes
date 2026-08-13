namespace Hermes.Domain.Exceptions;

/// <summary>Wrong/expired verification code or missing challenge â†’ map to HTTP 400.</summary>
public sealed class VerificationCodeMismatchException : DomainException
{
    public VerificationCodeMismatchException()
        : base("Der Verifizierungscode stimmt nicht Ã¼berein.")
    {
    }

    public VerificationCodeMismatchException(string message)
        : base(message)
    {
    }
}
