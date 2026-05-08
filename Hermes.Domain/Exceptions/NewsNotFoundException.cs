namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when a news row is required but does not exist. Map to HTTP 404 at the API boundary.
/// </summary>
public sealed class NewsNotFoundException : Exception
{
    /// <summary>Initializes the exception with a default message.</summary>
    public NewsNotFoundException()
        : base("The requested news entry was not found.")
    {
    }

    /// <summary>Initializes the exception with a custom message.</summary>
    public NewsNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes the exception with a custom message and inner exception.</summary>
    public NewsNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
