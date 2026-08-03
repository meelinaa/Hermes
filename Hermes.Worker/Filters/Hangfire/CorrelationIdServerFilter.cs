using Hangfire.Server;
using Serilog.Context;

namespace Hermes.Worker.Filters.Hangfire;

/// <summary>
/// Hangfire server filter that extracts the CorrelationId from job parameters
/// and pushes it into the Serilog LogContext for the duration of job execution.
/// </summary>
public sealed class CorrelationIdServerFilter : IServerFilter
{
    /// <summary>
    /// Hangfire job parameter key used to pass the CorrelationId.
    /// </summary>
    public const string JOB_PARAMETER_NAME = "CorrelationId";

    private const string SCOPE_ITEM_KEY = "CorrelationIdLogScope";

    /// <summary>
    /// Called before the job is executed to push the CorrelationId into the Serilog LogContext scope.
    /// </summary>
    /// <param name="filterContext">The performing filter context containing job information.</param>
    public void OnPerforming(PerformingContext filterContext)
    {
        string? correlationId = filterContext.GetJobParameter<string>(JOB_PARAMETER_NAME);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            IDisposable scope = LogContext.PushProperty(JOB_PARAMETER_NAME, correlationId);
            filterContext.Items[SCOPE_ITEM_KEY] = scope;
        }
    }

    /// <summary>
    /// Called after the job has completed to dispose the Serilog LogContext scope.
    /// </summary>
    /// <param name="filterContext">The performed filter context containing job execution results.</param>
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(SCOPE_ITEM_KEY, out object? scopeObj) && scopeObj is IDisposable scope)
        {
            scope.Dispose();
        }
    }
}
