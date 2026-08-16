using System.Diagnostics;
using Hangfire.Client;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Hangfire client filter that extracts the active W3C trace context from the calling thread
/// and attaches it as Hangfire job parameters to enable distributed tracing across service boundaries.
/// </summary>
public sealed class HangfireTraceContextClientFilter : IClientFilter
{
    /// <summary>
    /// Hangfire job parameter key used to store the W3C traceparent header string.
    /// </summary>
    public const string TRACE_PARENT_PARAMETER_NAME = "TraceParent";

    /// <summary>
    /// Hangfire job parameter key used to store the optional W3C tracestate header string.
    /// </summary>
    public const string TRACE_STATE_PARAMETER_NAME = "TraceState";

    /// <summary>
    /// Captures the current W3C distributed trace context from <see cref="Activity.Current"/> prior to job creation.
    /// Attaches traceparent and tracestate as job parameters so background workers can link child spans.
    /// </summary>
    /// <param name="filterContext">The Hangfire client filter context for job creation.</param>
    public void OnCreating(CreatingContext filterContext)
    {
        Activity? currentActivity = Activity.Current;
        if (currentActivity is null)
            return;

        string? traceParent = currentActivity.Id;
        if (!string.IsNullOrWhiteSpace(traceParent))
        {
            filterContext.SetJobParameter(TRACE_PARENT_PARAMETER_NAME, traceParent);
        }

        string? traceState = currentActivity.TraceStateString;
        if (!string.IsNullOrWhiteSpace(traceState))
        {
            filterContext.SetJobParameter(TRACE_STATE_PARAMETER_NAME, traceState);
        }
    }

    /// <summary>
    /// Post-creation callback for the Hangfire client filter. No action required after job registration.
    /// </summary>
    /// <param name="filterContext">The Hangfire client filter created context.</param>
    public void OnCreated(CreatedContext filterContext)
    {
        // No action required after creation.
    }
}
