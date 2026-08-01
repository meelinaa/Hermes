using Hangfire.Server;
using Serilog.Context;

namespace Hermes.Worker.Hangfire;

/// <summary>
/// Hangfire server filter that extracts the CorrelationId from the job parameters
/// and pushes it into the Serilog LogContext for the duration of the job execution.
/// </summary>
public sealed class CorrelationIdServerFilter : IServerFilter
{
    public const string JOB_PARAMETER_NAME = "CorrelationId";
    private const string SCOPE_ITEM_KEY = "CorrelationIdLogScope";

    public void OnPerforming(PerformingContext filterContext)
    {
        string? correlationId = filterContext.GetJobParameter<string>(JOB_PARAMETER_NAME);

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            IDisposable scope = LogContext.PushProperty(JOB_PARAMETER_NAME, correlationId);
            filterContext.Items[SCOPE_ITEM_KEY] = scope;
        }
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(SCOPE_ITEM_KEY, out object? scopeObj) && scopeObj is IDisposable scope)
        {
            scope.Dispose();
        }
    }
}
