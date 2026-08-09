using Microsoft.Extensions.Logging;

namespace Hermes.Application.Logging;

/// <summary>
/// Source-generated logger methods for authentication and security related events.
/// </summary>
public static partial class AuthLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Replay detected: Attempt to rotate revoked or expired token. UserId: {UserId}, TokenHash: {TokenHash}")]
    public static partial void LogReplayDetected(this ILogger logger, int userId, string tokenHash);
}
