using Hangfire.Client;
using Hermes.Api.Middleware;

namespace Hermes.Api.Hangfire;

/// <summary>
/// Hangfire client filter that extracts the CorrelationId from the current HTTP request
/// and attaches it as a job parameter so it can be propagated to the background worker.
/// </summary>
public sealed class CorrelationIdClientFilter(IHttpContextAccessor httpContextAccessor) : IClientFilter
{
    public const string JOB_PARAMETER_NAME = "CorrelationId";

    public void OnCreating(CreatingContext filterContext)
    {
        string? correlationId = httpContextAccessor.HttpContext?.Items[CorrelationIdMiddleware.HTTP_CONTEXT_ITEM_KEY]?.ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            filterContext.SetJobParameter(JOB_PARAMETER_NAME, correlationId);
        }
    }

    public void OnCreated(CreatedContext filterContext)
    {
        // No action required after creation.
    }
}
