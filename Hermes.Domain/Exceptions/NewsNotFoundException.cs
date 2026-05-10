namespace Hermes.Domain.Exceptions;

/// <summary>News row missing → map to HTTP 404 at the API boundary.</summary>
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
