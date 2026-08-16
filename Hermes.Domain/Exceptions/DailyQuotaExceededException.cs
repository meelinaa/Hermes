namespace Hermes.Domain.Exceptions;

/// <summary>
/// Exception thrown when the external provider daily request quota has been exhausted.
/// Indicates a non-transient condition that should bypass standard retry pipelines.
/// </summary>
public sealed class DailyQuotaExceededException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DailyQuotaExceededException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public DailyQuotaExceededException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyQuotaExceededException"/> class with default message.
    /// </summary>
    public DailyQuotaExceededException() : base("External news provider daily quota has been exceeded.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DailyQuotaExceededException"/> class with a specified message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The inner exception.</param>
    public DailyQuotaExceededException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
