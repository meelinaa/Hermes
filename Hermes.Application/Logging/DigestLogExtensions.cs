using Microsoft.Extensions.Logging;

namespace Hermes.Application.Logging;

/// <summary>
/// Source-generated logger methods for digest and email notifications.
/// </summary>
public static partial class DigestLogExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Verification e-mail sending for user {UserId} was canceled.")]
    public static partial void LogVerificationCanceled(this ILogger logger, int userId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to send verification e-mail for user {UserId}.")]
    public static partial void LogVerificationFailed(this ILogger logger, Exception exception, int userId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Newsletter digest sending for user {UserId}, news {NewsId} was canceled.")]
    public static partial void LogNewsletterDigestCanceled(this ILogger logger, int userId, int newsId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to send newsletter digest for user {UserId}, news {NewsId}.")]
    public static partial void LogNewsletterDigestFailed(this ILogger logger, Exception exception, int userId, int newsId);
}
