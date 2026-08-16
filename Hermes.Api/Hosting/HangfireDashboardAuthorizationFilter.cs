using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Hermes.Api.Hosting;

/// <summary>
/// Hangfire dashboard authorization filter that restricts access to the management UI in non-development environments.
/// Ensures that unauthenticated or unauthorized third parties cannot inspect or manipulate background job queues.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter(IHostEnvironment environment) : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Evaluates whether the incoming HTTP request is authorized to view and interact with the Hangfire Dashboard.
    /// </summary>
    /// <param name="context">The Hangfire dashboard request context.</param>
    /// <returns>True if the request is permitted; otherwise false.</returns>
    public bool Authorize(DashboardContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (environment.IsDevelopment())
            return true;

        HttpContext httpContext = context.GetHttpContext();
        if (httpContext is null)
            return false;

        // In production/staging, require authenticated user with valid identity
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
