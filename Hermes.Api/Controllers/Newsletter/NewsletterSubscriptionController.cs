using FluentResults;
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
using Hermes.Domain.ValueObjects;

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

        Result<NewsletterSubscriptionListResultDto> result = await newsService.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
        if (result.IsFailed)
            return this.BadRequestProblem(result.Errors.First().Message);

        return Ok(new PagedNewsletterSubscriptionListResponseDto(
            result.Value.Items.Select(static n => n.ToResponse()).ToList(),
            result.Value.Page,
            result.Value.PageSize,
            result.Value.TotalCount,
            result.Value.TotalPages,
            result.Value.HasNextPage,
            result.Value.NextAfterId));
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
        Result<NewsletterSubscription> newsResult = await newsService.GetNewsByIdAsync(new UserId(userId), new NewsletterId(newsId), cancellationToken).ConfigureAwait(false);
        return newsResult.IsFailed ? this.NotFoundProblem() : Ok(newsResult.Value.ToResponse());
    }

    /// <summary>
    /// Registers a new newsletter configuration for the specified user.
    /// Automatically triggers a background evaluation to calculate the first delivery slot based on the user's schedule.
    /// Enforces IDOR authorization matching caller identity against route parameter userId.
    /// </summary>
    /// <param name="userId">The owner user ID in the URL path.</param>
    /// <param name="request">The newsletter configuration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 201 Created result containing the created subscription identifiers.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost("{userId:int}/newsletter-subscriptions")]
    public async Task<ActionResult<CreateNewsletterSubscriptionResponseDto>> SetNews(
        int userId,
        [FromBody] CreateNewsletterSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        NewsletterSubscription entity = request.ToEntity(new UserId(userId));
        Result<NewsletterId> setNewsResult = await newsService.SetNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        if (setNewsResult.IsFailed)
            return this.BadRequestProblem(setNewsResult.Errors.First().Message);
            
        int newsId = setNewsResult.Value.Value;
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return CreatedAtAction(nameof(GetNewsById), new { userId, newsId }, new CreateNewsletterSubscriptionResponseDto(userId, newsId));
    }

    /// <summary>
    /// Overwrites an existing subscription's rules, such as keywords, categories, or schedules.
    /// Forces an immediate recalculation of the next delivery window to reflect schedule modifications.
    /// Enforces IDOR authorization matching caller identity against route parameter userId.
    /// </summary>
    /// <param name="userId">The owner user ID in the URL path.</param>
    /// <param name="newsId">The newsletter subscription ID in the URL path.</param>
    /// <param name="request">The updated subscription configuration payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 204 No Content result upon successful update.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("{userId:int}/newsletter-subscriptions/{newsId:int}")]
    public async Task<ActionResult> UpdateNews(
        int userId,
        int newsId,
        [FromBody] UpdateNewsletterSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        Result<NewsletterSubscription> existingResult = await newsService.FindNewsByIdAsync(new NewsletterId(newsId), cancellationToken).ConfigureAwait(false);
        if (existingResult.IsFailed)
            return this.NotFoundProblem();

        if (existingResult.Value.UserId.Value != userId)
            return this.ForbiddenProblem("You can only access resources for your own account.");

        NewsletterSubscription entity = request.ToEntity(new UserId(userId), existingResult.Value);
        Result updateResult = await newsService.UpdateNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        if (updateResult.IsFailed)
            return this.BadRequestProblem(updateResult.Errors.First().Message);

        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return NoContent();
    }

    /// <summary>
    /// Wipes all newsletter configurations for a user.
    /// Typically invoked during account deletion or as a bulk reset action by the user.
    /// </summary>
    /// <param name="userId">The owner user ID in the URL path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A response containing the total count of deleted subscriptions.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/newsletter-subscriptions")]
    public async Task<ActionResult<DeleteAllNewsletterSubscriptionResponseDto>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        Result<int> deleteResult = await newsService.DeleteAllNewsByUserAsync(new UserId(userId), cancellationToken).ConfigureAwait(false);
        int deleted = deleteResult.IsSuccess ? deleteResult.Value : 0;
        return Ok(new DeleteAllNewsletterSubscriptionResponseDto(deleted));
    }

    /// <summary>
    /// Removes a specific subscription.
    /// Stops any further email deliveries for this particular news topic configuration.
    /// </summary>
    /// <param name="userId">The owner user ID in the URL path.</param>
    /// <param name="newsId">The newsletter subscription ID in the URL path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 204 No Content result upon successful deletion.</returns>
    [Authorize(Policy = HermesAuthorizationPolicyConstants.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/newsletter-subscriptions/{newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        Result<NewsletterSubscription> deleteResult = await newsService.GetNewsByIdAsync(new UserId(userId), new NewsletterId(newsId), cancellationToken).ConfigureAwait(false);
        if (deleteResult.IsFailed)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteResult.Value, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return NoContent();
    }
}
