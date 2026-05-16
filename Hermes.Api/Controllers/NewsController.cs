using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Authorization;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Api.Validation;
using Hermes.Application.Models.News;
using Hermes.Application.Options;
using Hermes.Application.Scheduling;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Hermes.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/users")]
public class NewsController(
    INewsService newsService,
    INewsletterSchedulerRunTrigger newsletterSchedulerRunTrigger,
    IOptions<PaginationOptions> paginationOptions) : ControllerBase
{
    /// <summary>List: <c>afterId</c> cursor only with ascending <c>sort</c> (not with <c>-id</c>).</summary>
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

        try
        {
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
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
    }

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

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [HttpGet("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult<NewsResponse>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        try
        {
            News? news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
            return news is null ? this.NotFoundProblem() : Ok(news.ToResponse());
        }
        catch (NewsNotFoundException)
        {
            return this.NotFoundProblem();
        }
    }

    /// <remarks><b>POST</b> <c>/api/v1/users/news</c>: body omits <c>userId</c> (from JWT).</remarks>
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPost("news")]
    public async Task<ActionResult<CreateNewsResponse>> SetNews(
        [FromBody] CreateNewsRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        News entity = request.ToEntity(currentUserId);
        try
        {
            int newsId = await newsService.SetNewsAsync(entity, cancellationToken).ConfigureAwait(false);
            newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
            return Ok(new CreateNewsResponse(currentUserId, newsId));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }
    }

    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpPut("news")]
    public async Task<ActionResult> UpdateNews(
        [FromBody] UpdateNewsRequest request,
        [FromServices] IValidator<UpdateNewsRequest> validator,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out int currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        ValidationResult fv = await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        News? existing;
        try
        {
            existing = await newsService.GetNewsByIdAsync(currentUserId, request.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (NewsNotFoundException)
        {
            return this.NotFoundProblem();
        }

        if (existing is null)
            return this.NotFoundProblem();

        News entity = request.ToEntity(currentUserId, existing);
        try
        {
            await newsService.UpdateNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (NewsAccessDeniedException)
        {
            return Problem(title: "News access denied.", statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return ValidationProblem(ModelState);
        }

        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/news/all")]
    public async Task<ActionResult<DeleteAllNewsResponse>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        int deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeleteAllNewsResponse(deleted));
    }

    [Authorize(Policy = HermesAuthorizationPolicies.OWN_USER_ROUTE_USER_ID)]
    [EnableRateLimiting("SensitiveWritePolicy")]
    [HttpDelete("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        News? deleteNews;
        try
        {
            deleteNews = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        }
        catch (NewsNotFoundException)
        {
            return this.NotFoundProblem();
        }

        if (deleteNews is null)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }
}
