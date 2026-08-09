using Microsoft.Extensions.Logging;

namespace Hermes.Application.Logging;

/// <summary>
/// Provides shared, cross-cutting source-generated logger methods for common application errors.
/// </summary>
public static partial class SharedLogs
{
    /// <summary>
    /// Logs a generic error message indicating that an unexpected operation failure occurred.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="message">The specific error message context.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "{Message}")]
    public static partial void LogGenericError(this ILogger logger, Exception ex, string message);
}
