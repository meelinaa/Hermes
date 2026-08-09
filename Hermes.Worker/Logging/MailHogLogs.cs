using Microsoft.Extensions.Logging;

namespace Hermes.Worker.Logging;

public static partial class MailHogLogs
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "[MailHog] Scheduler-Testmail gesendet an {Address} (Absender wie in Email:DefaultFromAddress).")]
    public static partial void LogTestMailSent(this ILogger logger, string address);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "SMTP: {Host}:{Port} (SSL={Ssl}), From={From} — für lokales MailHog typisch Port 1025.")]
    public static partial void LogSmtpInfo(this ILogger logger, string host, int port, bool ssl, string from);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "MailHog-Web-UI: {BaseUrl}")]
    public static partial void LogMailHogWebUi(this ILogger logger, string baseUrl);
}
