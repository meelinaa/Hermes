namespace Hermes.Domain.Exceptions;

/// <summary>
/// Thrown when a news row is required but does not exist. Map to HTTP 404 at the API boundary.
/// </summary>
public sealed class NewsNotFoundException : Exception
{
    public NewsNotFoundException()
        : base("The requested news entry was not found.")
    {
    }

    public NewsNotFoundException(string message)
        : base(message)
    {
    }

    public NewsNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
