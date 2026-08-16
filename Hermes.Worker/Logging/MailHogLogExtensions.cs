using Microsoft.Extensions.Logging;

namespace Hermes.Worker.Logging;

public static partial class SmtpLogExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "SMTP: {Host}:{Port} (SSL={Ssl}), From={From}")]
    public static partial void LogSmtpInfo(this ILogger logger, string host, int port, bool ssl, string from);
}
