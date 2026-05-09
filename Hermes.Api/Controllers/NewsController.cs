using FluentValidation;
using FluentValidation.Results;
using Hermes.Api.Http;
using Hermes.Api.Mapping;
using Hermes.Api.Validation;
using Hermes.Application.Models.News;
using Hermes.Application.Scheduling;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>
/// News resources under <c>/api/v1/users/…</c>; collection/item URLs use normal path segments (no query-like literals).
/// Create/update bodies omit owning <c>userId</c> (JWT).
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/users")]
public class NewsController(
    INewsService newsService,
    INewsletterSchedulerRunTrigger newsletterSchedulerRunTrigger) : ControllerBase
{
    /// <summary>Returns all news entries for the user.</summary>
    /// <remarks><b>GET</b> <c>/api/v1/users/{userId}/news</c> — no body.</remarks>
    [HttpGet("{userId:int}/news")]
    public async Task<ActionResult<List<NewsResponse>>> GetNewsList(int userId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        List<News> list = await newsService.GetAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(list.ConvertAll(static entity => entity.ToResponse()));
    }

    /// <summary>Returns a single news row for the user.</summary>
    /// <remarks><b>GET</b> <c>/api/v1/users/{userId}/news/{newsId}</c> — no body.</remarks>
    [HttpGet("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult<NewsResponse>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

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

    /// <summary>Create news for the authenticated user (owner from JWT, not request body).</summary>
    /// <remarks>
    /// <b>POST</b> <c>/api/v1/users/news</c> — Body omits <c>userId</c>. Enum fields use underlying integer values or names (see <see cref="Hermes.Domain.Enums"/>).
    /// </remarks>
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

    /// <summary>Update news; <c>id</c> required in body; owner from JWT.</summary>
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

        News entity = request.ToEntity(currentUserId);
        await newsService.UpdateNewsAsync(entity, cancellationToken).ConfigureAwait(false);
        newsletterSchedulerRunTrigger.RequestRunAfterNewsMutation();
        return Ok();
    }

    /// <summary>Delete all news rows for this user. No body.</summary>
    /// <remarks><b>DELETE</b> <c>/api/v1/users/{userId}/news/all</c></remarks>
    [HttpDelete("{userId:int}/news/all")]
    public async Task<ActionResult<DeleteAllNewsResponse>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        int deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new DeleteAllNewsResponse(deleted));
    }

    /// <remarks><b>DELETE</b> <c>/api/v1/users/{userId}/news/{newsId}</c> — no body.</remarks>
    [HttpDelete("{userId:int}/news/{newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

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
