namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when the caller may not access or modify a news row (e.g. wrong owner). Map to HTTP 403 at the API boundary.
/// </summary>
public sealed class NewsAccessDeniedException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public NewsAccessDeniedException()
        : base("You do not have permission to access this news entry.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public NewsAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a custom message and inner exception.</summary>
    public NewsAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
