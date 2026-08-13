using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

using Hermes.Api.Constants;
using Hermes.Api.Http;
using Hermes.Api.Mapping.Newsletter;
using Hermes.Application.DTOs.NewsletterSubscription;
using Hermes.Application.Options.Common;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;

namespace Hermes.Api.Controllers.Newsletter;

/// <summary>
/// Exposes endpoints to manage newsletter configurations. 
/// Enables users to customize topics, frequency, and delivery times for their personal news digests.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class NewsletterSubscriptionController(
    INewsletterSubscriptionService newsService,
    INewsletterSchedulerJobService newsletterSchedulerRunTrigger,
    IOptions<PaginationOptions> paginationOptions) : ControllerBase
{
    /// <summary>
    /// Returns a paginated overview of the user's active and inactive subscriptions.
    /// Allows client UIs to display a comprehensive dashboard of configured news streams,
    /// supporting both offset and cursor-based pagination for large datasets.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/newsletter-subscriptions")]
    public async Task<ActionResult<PagedNewsletterSubscriptionListResponseDto>> GetNewsList(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null,
        [FromQuery] int? afterId = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? q = null,
        [FromQuery] NewsCategory? category = null,
        CancellationToken cancellationToken = default)
    {
        PaginationOptions po = paginationOptions.Value;
        int size = pageSize ?? po.DefaultPageSize;
        if (size < 1)
        {
            ModelState.AddModelError(nameof(pageSize), "Page size must be at least 1.");
            return ValidationProblem(ModelState);
        }

        if (size > po.MaxPageSize)
            size = po.MaxPageSize;

        if (page < 1)
        {
            ModelState.AddModelError(nameof(page), "Page must be at least 1.");
            return ValidationProblem(ModelState);
        }

        if (!TryParseSort(sort, out bool sortDescending, out string? sortError))
        {
            ModelState.AddModelError(nameof(sort), sortError ?? "Invalid sort.");
            return ValidationProblem(ModelState);
        }

        if (afterId is not null and < 0)
        {
            ModelState.AddModelError(nameof(afterId), "afterId must be non-negative.");
            return ValidationProblem(ModelState);
        }

        string? search = null;
        if (!string.IsNullOrWhiteSpace(q))
        {
            search = q.Trim();
            if (search.Length > 200)
                search = search[..200];
        }

        int effectivePage = afterId is not null ? 1 : page;

        NewsletterSubscriptionListQueryDto query = new(
            userId,
            effectivePage,
            size,
            afterId,
            sortDescending,
            search,
            category);

        NewsletterSubscriptionListResultDto result = await newsService.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(new PagedNewsletterSubscriptionListResponseDto(
            result.Items.Select(static n => n.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.NextAfterId));
    }

    /// <summary>
    /// Normalizes sorting query parameters into boolean flags used by the data access layer.
    /// </summary>
    private static bool TryParseSort(string? sort, out bool sortDescending, out string? error)
    {
        sortDescending = false;
        error = null;
        if (string.IsNullOrWhiteSpace(sort))
            return true;
        if (sort.Equals("id", StringComparison.OrdinalIgnoreCase))
            return true;
        if (sort.Equals("-id", StringComparison.OrdinalIgnoreCase))
        {
            sortDescending = true;
            return true;
        }

        error = "Use 'id' (ascending) or '-id' (descending).";
        return false;
    }

    /// <summary>
    /// Fetches the details of a specific subscription.
    /// Primarily used to populate edit forms on the client side with existing keywords and scheduling data.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/newsletter-subscriptions/{newsId:int}")]
    public async Task<ActionResult<NewsletterSubscriptionResponseDto>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        NewsletterSubscription? news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? this.NotFoundProblem() : Ok(news.ToResponse());
    }

    /// <summary>
    /// Registers a new newsletter configuration.
    /// Automatically triggers a background evaluation to calculate the first delivery slot based on the user's schedule.
    /// </summary>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost("newsletter-subscriptions")]
    public async Task<ActionResult<CreateNewsletterSubscriptionResponseDto>> SetNews(
        [FromBody] CreateNewsletterSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        NewsletterSubscription entity = request.ToEntity(currentUserId);
        int newsId = await newsService.SetNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok(new CreateNewsletterSubscriptionResponseDto(currentUserId, newsId));
    }

    /// <summary>
    /// Overwrites an existing subscription's rules, such as keywords, categories, or schedules.
    /// Forces an immediate recalculation of the next delivery window to reflect schedule modifications.
    /// </summary>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("newsletter-subscriptions")]
    public async Task<ActionResult> UpdateNews(
        [FromBody] UpdateNewsletterSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        NewsletterSubscription? existing = await newsService.FindNewsByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return this.NotFoundProblem();

        NewsletterSubscription entity = request.ToEntity(currentUserId, existing);
        await newsService.UpdateNewsAsync(entity, cancellationToken).ConfigureAwait(false);

        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }

    /// <summary>
    /// Wipes all newsletter configurations for a user.
    /// Typically invoked during account deletion or as a bulk reset action by the user.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/newsletter-subscriptions/all")]
    public async Task<ActionResult<DeleteAllNewsletterSubscriptionResponseDto>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        int deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeleteAllNewsletterSubscriptionResponseDto(deleted));
    }

    /// <summary>
    /// Removes a specific subscription.
    /// Stops any further email deliveries for this particular news topic configuration.
    /// </summary>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/newsletter-subscriptions/{newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        NewsletterSubscription? deleteNews = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (deleteNews is null)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }
}
