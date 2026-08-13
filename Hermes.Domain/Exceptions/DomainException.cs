using System;

namespace Hermes.Domain.Exceptions;

/// <summary>
/// Base class for all domain-specific exceptions.
/// These exceptions represent business rule violations and expected error conditions,
/// which should typically be translated into HTTP 400 (Bad Request) or other appropriate
/// standard responses at the API boundary (e.g. via RFC 7807 ProblemDetails).
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message)
        : base(message)
    {
    }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
