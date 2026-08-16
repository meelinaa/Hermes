using System;

namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when an aggregate root or entity fails domain validation rules.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message)
        : base(message)
    {
    }
}
