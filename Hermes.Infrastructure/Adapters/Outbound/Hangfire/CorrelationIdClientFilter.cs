using Hangfire.Client;
using Microsoft.AspNetCore.Http;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Hangfire client filter that extracts the CorrelationId from the current HTTP request
/// and attaches it as a job parameter so it can be propagated to the background worker.
/// </summary>
public sealed class CorrelationIdClientFilter(IHttpContextAccessor httpContextAccessor) : IClientFilter
{
    public const string JOB_PARAMETER_NAME = "CorrelationId";
    public const string HTTP_CONTEXT_ITEM_KEY = "CorrelationId";

    /// <summary>
    /// Captures correlation ID from HTTP context items before Hangfire job creation.
    /// </summary>
    /// <param name="filterContext">The Hangfire client filter creation context.</param>
    public void OnCreating(CreatingContext filterContext)
    {
        string? correlationId = httpContextAccessor.HttpContext?.Items[HTTP_CONTEXT_ITEM_KEY]?.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            filterContext.SetJobParameter(JOB_PARAMETER_NAME, correlationId);
        }
    }

    /// <summary>
    /// Post-creation callback for Hangfire client filter.
    /// </summary>
    /// <param name="filterContext">The Hangfire client filter created context.</param>
    public void OnCreated(CreatedContext filterContext)
    {
        // No action required after creation.
    }
}
