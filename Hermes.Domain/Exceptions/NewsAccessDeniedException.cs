namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when the caller may not access or modify a news row (e.g. wrong owner). Map to HTTP 403 at the API boundary.
/// </summary>
public sealed class NewsAccessDeniedException : Exception
{
    public NewsAccessDeniedException()
        : base("You do not have permission to access this news entry.")
    {
    }

    public NewsAccessDeniedException(string message)
        : base(message)
    {
    }

    public NewsAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
