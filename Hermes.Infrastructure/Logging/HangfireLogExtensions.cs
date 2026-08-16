using Microsoft.Extensions.Logging;

namespace Hermes.Infrastructure.Logging;

public static partial class HangfireLogExtensions 
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Newsletter scheduler execution requested manually for job {JobId}")]
    public static partial void LogManualSchedulerExecutionRequested(this ILogger logger, string jobId);
}
