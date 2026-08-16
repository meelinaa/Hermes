using System;

namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when an email address is invalid (empty, too long, or malformed).
/// </summary>
public sealed class InvalidEmailException : DomainException
{
    public InvalidEmailException(string message)
        : base(message)
    {
    }
}
