using Hangfire.Server;
using Serilog.Context;

namespace Hermes.Worker.Filters.Hangfire;

/// <summary>
/// Hangfire server filter that extracts or generates a CorrelationId from job parameters
/// and pushes it alongside the HangfireJobId into the Serilog LogContext for the duration of job execution.
/// </summary>
public sealed class CorrelationIdServerFilter : IServerFilter
{
    /// <summary>
    /// Hangfire job parameter key used to pass the CorrelationId.
    /// </summary>
    public const string JOB_PARAMETER_NAME = "CorrelationId";

    /// <summary>
    /// LogContext property key used for the Hangfire job identifier.
    /// </summary>
    public const string HANGFIRE_JOB_ID_PROPERTY_NAME = "HangfireJobId";

    private const string SCOPE_ITEM_KEY = "CorrelationIdLogScope";

    /// <summary>
    /// Intercepts job execution to push the CorrelationId and HangfireJobId into the Serilog LogContext scope.
    /// If no CorrelationId was supplied by the caller, a deterministic fallback correlation ID is generated.
    /// </summary>
    /// <param name="filterContext">The performing filter context containing job information.</param>
    public void OnPerforming(PerformingContext filterContext)
    {
        string correlationId = filterContext.GetJobParameter<string>(JOB_PARAMETER_NAME)
            ?? $"job-{filterContext.BackgroundJob.Id}-{Guid.NewGuid():N}";

        IDisposable correlationScope = LogContext.PushProperty(JOB_PARAMETER_NAME, correlationId);
        IDisposable jobScope = LogContext.PushProperty(HANGFIRE_JOB_ID_PROPERTY_NAME, filterContext.BackgroundJob.Id);

        filterContext.Items[SCOPE_ITEM_KEY] = new CompositeLogScope(correlationScope, jobScope);
    }

    /// <summary>
    /// Disposes the Serilog LogContext scopes after the job has completed.
    /// </summary>
    /// <param name="filterContext">The performed filter context containing job execution results.</param>
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(SCOPE_ITEM_KEY, out object? scopeObj) && scopeObj is IDisposable scope)
        {
            scope.Dispose();
        }
    }

    private sealed class CompositeLogScope(IDisposable scope1, IDisposable scope2) : IDisposable
    {
        public void Dispose()
        {
            scope1.Dispose();
            scope2.Dispose();
        }
    }
}

