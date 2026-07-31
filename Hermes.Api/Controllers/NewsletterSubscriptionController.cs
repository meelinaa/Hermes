using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Application.Models.NewsletterSubscription;
using Hermes.Application.Options;
using Hermes.Application.Scheduling;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Hermes.Api.Controllers;

/// <summary>
/// Controller for managing newsletter subscription profiles and schedules.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class NewsletterSubscriptionController(
    INewsletterSubscriptionService newsService,
    INewsletterSchedulerRunTrigger newsletterSchedulerRunTrigger,
    IOptions<PaginationOptions> paginationOptions) : ControllerBase
{
    /// <summary>
    /// Retrieves a paged list of newsletter subscriptions for a given user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="page">The page number (defaults to 1).</param>
    /// <param name="pageSize">The page size (defaults to configuration limit).</param>
    /// <param name="afterId">Optional cursor identifier for keyset paging.</param>
    /// <param name="sort">Sort order direction, e.g. "id" or "-id".</param>
    /// <param name="q">Optional search query term.</param>
    /// <param name="category">Optional news category filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of newsletter subscription profiles.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/newsletter-subscriptions")]
    public async Task<ActionResult<PagedNewsletterSubscriptionListResponse>> GetNewsList(
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

        NewsletterSubscriptionListQuery query = new(
            userId,
            effectivePage,
            size,
            afterId,
            sortDescending,
            search,
            category);

        NewsletterSubscriptionListResult result = await newsService.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(new PagedNewsletterSubscriptionListResponse(
            result.Items.Select(static n => n.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.NextAfterId));
    }

    /// <summary>
    /// Parses the sorting parameter into internal flags.
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
    /// Retrieves a single newsletter subscription profile by its ID.
    /// </summary>
    /// <param name="userId">The ID of the user owning the profile.</param>
    /// <param name="newsId">The ID of the newsletter subscription.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newsletter subscription details.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/newsletter-subscriptions/{newsId:int}")]
    public async Task<ActionResult<NewsletterSubscriptionResponse>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        NewsletterSubscription? news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? this.NotFoundProblem() : Ok(news.ToResponse());
    }

    /// <summary>
    /// Creates a new newsletter subscription profile for the authenticated user.
    /// </summary>
    /// <param name="request">The parameters of the newsletter subscription to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A response containing the created subscription identifier.</returns>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost("newsletter-subscriptions")]
    public async Task<ActionResult<CreateNewsletterSubscriptionResponse>> SetNews(
        [FromBody] CreateNewsletterSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        NewsletterSubscription entity = request.ToEntity(currentUserId);
        int newsId = await newsService.SetNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok(new CreateNewsletterSubscriptionResponse(currentUserId, newsId));
    }

    /// <summary>
    /// Updates an existing newsletter subscription profile.
    /// </summary>
    /// <param name="request">The updated properties of the newsletter subscription.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An OK result if updated successfully.</returns>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("newsletter-subscriptions")]
    public async Task<ActionResult> UpdateNews(
        [FromBody] UpdateNewsletterSubscriptionRequest request,
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
    /// Deletes all newsletter subscriptions for the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The delete status summary.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/newsletter-subscriptions/all")]
    public async Task<ActionResult<DeleteAllNewsletterSubscriptionResponse>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        int deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeleteAllNewsletterSubscriptionResponse(deleted));
    }

    /// <summary>
    /// Deletes a specific newsletter subscription profile.
    /// </summary>
    /// <param name="userId">The ID of the user owning the profile.</param>
    /// <param name="newsId">The ID of the newsletter subscription to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An OK result if deleted successfully.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
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
