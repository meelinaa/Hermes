using System.Diagnostics;
using Hangfire.Server;
using OpenTelemetry.Trace;

namespace Hermes.Worker.Filters.Hangfire;

/// <summary>
/// Hangfire server filter that extracts W3C trace context from job parameters and starts
/// a linked child span under the <c>Hermes.Hangfire</c> <see cref="ActivitySource"/> to ensure
/// seamless distributed trace continuity between API HTTP requests and asynchronous Worker executions.
/// </summary>
public sealed class HangfireTraceContextServerFilter : IServerFilter
{
    /// <summary>
    /// The name of the <see cref="ActivitySource"/> used for Hangfire background job tracing spans.
    /// </summary>
    public const string ACTIVITY_SOURCE_NAME = "Hermes.Hangfire";

    /// <summary>
    /// Hangfire job parameter key used to retrieve the W3C traceparent header string.
    /// </summary>
    public const string TRACE_PARENT_PARAMETER_NAME = "TraceParent";

    /// <summary>
    /// Hangfire job parameter key used to retrieve the optional W3C tracestate header string.
    /// </summary>
    public const string TRACE_STATE_PARAMETER_NAME = "TraceState";

    private const string ACTIVITY_ITEM_KEY = "HangfireTraceActivity";

    private static readonly ActivitySource s_activitySource = new(ACTIVITY_SOURCE_NAME, "1.0.0");

    /// <summary>
    /// Intercepts job execution before the target method runs, restoring the parent W3C trace context
    /// if present and starting a dedicated consumer activity with relevant diagnostic tags.
    /// </summary>
    /// <param name="filterContext">The performing filter context containing job metadata and parameters.</param>
    public void OnPerforming(PerformingContext filterContext)
    {
        string? traceParent = filterContext.GetJobParameter<string>(TRACE_PARENT_PARAMETER_NAME);
        string? traceState = filterContext.GetJobParameter<string>(TRACE_STATE_PARAMETER_NAME);

        string jobName = filterContext.BackgroundJob.Job is not null
            ? $"{filterContext.BackgroundJob.Job.Type.Name}.{filterContext.BackgroundJob.Job.Method.Name}"
            : filterContext.BackgroundJob.Id;

        Activity? activity;

        if (!string.IsNullOrWhiteSpace(traceParent) && ActivityContext.TryParse(traceParent, traceState, out ActivityContext parentContext))
        {
            activity = s_activitySource.StartActivity($"Hangfire {jobName}", ActivityKind.Consumer, parentContext);
        }
        else
        {
            activity = s_activitySource.StartActivity($"Hangfire {jobName}", ActivityKind.Internal);
        }

        if (activity is not null)
        {
            activity.SetTag("hangfire.job.id", filterContext.BackgroundJob.Id);
            activity.SetTag("hangfire.job.type", filterContext.BackgroundJob.Job?.Type.FullName ?? string.Empty);
            activity.SetTag("hangfire.job.method", filterContext.BackgroundJob.Job?.Method.Name ?? string.Empty);
            filterContext.Items[ACTIVITY_ITEM_KEY] = activity;
        }
    }

    /// <summary>
    /// Intercepts job execution after the target method completes, recording any unhandled exceptions,
    /// setting span status, and cleanly disposing the active activity.
    /// </summary>
    /// <param name="filterContext">The performed filter context containing execution results and exception details.</param>
    public void OnPerformed(PerformedContext filterContext)
    {
        if (filterContext.Items.TryGetValue(ACTIVITY_ITEM_KEY, out object? actObj) && actObj is Activity activity)
        {
            if (filterContext.Exception is not null && !filterContext.ExceptionHandled)
            {
                activity.SetStatus(ActivityStatusCode.Error, filterContext.Exception.Message);
                activity.AddException(filterContext.Exception);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            activity.Dispose();
        }
    }
}
