using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Application.Models.News;
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
/// Controller for managing news subscription profiles and schedules.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class NewsController(
    INewsService newsService,
    INewsletterSchedulerRunTrigger newsletterSchedulerRunTrigger,
    IOptions<PaginationOptions> paginationOptions) : ControllerBase
{
    /// <summary>
    /// Retrieves a paged list of news configurations for a given user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="page">The page number (defaults to 1).</param>
    /// <param name="pageSize">The page size (defaults to configuration limit).</param>
    /// <param name="afterId">Optional cursor identifier for keyset paging.</param>
    /// <param name="sort">Sort order direction, e.g. "id" or "-id".</param>
    /// <param name="q">Optional search query term.</param>
    /// <param name="category">Optional news category filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged list of news profiles.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/news")]
    public async Task<ActionResult<PagedNewsListResponse>> GetNewsList(
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

        NewsListQuery query = new(
            userId,
            effectivePage,
            size,
            afterId,
            sortDescending,
            search,
            category);

        NewsListResult result = await newsService.GetNewsListAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(new PagedNewsListResponse(
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
    /// Retrieves a single news configuration profile by its ID.
    /// </summary>
    /// <param name="userId">The ID of the user owning the profile.</param>
    /// <param name="newsId">The ID of the news configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The news configuration details.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult<NewsResponse>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        News? news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? this.NotFoundProblem() : Ok(news.ToResponse());
    }

    /// <summary>
    /// Creates a new news configuration subscription for the authenticated user.
    /// </summary>
    /// <param name="request">The parameters of the news configuration to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A response containing the created configuration identifier.</returns>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost("news")]
    public async Task<ActionResult<CreateNewsResponse>> SetNews(
        [FromBody] CreateNewsRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        News entity = request.ToEntity(currentUserId);
        int newsId = await newsService.SetNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok(new CreateNewsResponse(currentUserId, newsId));
    }

    /// <summary>
    /// Updates an existing news subscription profile.
    /// </summary>
    /// <param name="request">The updated properties of the news profile.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An OK result if updated successfully.</returns>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("news")]
    public async Task<ActionResult> UpdateNews(
        [FromBody] UpdateNewsRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        News? existing = await newsService.FindNewsByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return this.NotFoundProblem();

        News entity = request.ToEntity(currentUserId, existing);
        await newsService.UpdateNewsAsync(entity, cancellationToken).ConfigureAwait(false);

        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }

    /// <summary>
    /// Deletes all news configurations for the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The delete status summary.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/news/all")]
    public async Task<ActionResult<DeleteAllNewsResponse>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        int deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeleteAllNewsResponse(deleted));
    }

    /// <summary>
    /// Deletes a specific news subscription configuration.
    /// </summary>
    /// <param name="userId">The ID of the user owning the profile.</param>
    /// <param name="newsId">The ID of the news configuration to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An OK result if deleted successfully.</returns>
    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        News? deleteNews = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (deleteNews is null)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }
}
