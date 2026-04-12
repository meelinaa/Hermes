using FluentValidation;
using Hermes.Api.Http;
using Hermes.Api.Validation;
using Hermes.Application.Services;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>News under <c>api/v1/users/news/…</c>; resource ids in the path for safe GET/DELETE where applicable.</summary>
[Authorize]
[ApiController]
[Route("api/v1/users/news")]
public class NewsController(INewsService newsService) : ControllerBase
{
    /// <remarks><b>GET</b> <c>api/v1/users/news/{userId}/list</c> — no body.</remarks>
    [HttpGet("{userId}/list")]
    public async Task<ActionResult<List<News>>> GetNewsList(int userId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        var list = await newsService.GetAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    /// <remarks>
    /// <b>GET</b> <c>api/v1/users/news/userId={userId}/newsId={newsId}</c> — no body.
    /// Uses composite path segments (literal + value) so ids are named in the URL; e.g. <c>…/userId=1/newsId=5</c>.
    /// </remarks>
    [HttpGet("userId={userId:int}/newsId={newsId:int}")]
    public async Task<ActionResult<News>> GetNewsById(int userId, int newsId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        var news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? this.NotFoundProblem() : Ok(news);
    }

    /// <summary>Create news for <paramref name="userId"/>.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users/news</c> — Body (<c>userId</c> &gt; 0). Enum fields use underlying integer values (see <see cref="Hermes.Domain.Enums"/>).
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<NewsScope>> SetNews([FromBody] News news, CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out var currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        if (news.UserId <= 0)
            news.UserId = currentUserId;
        else if (news.UserId != currentUserId)
            return this.ForbiddenProblem("Body userId must match the authenticated user (or omit/zero to use your account).");

        int newsId = await newsService.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        NewsScope scope = new() { UserId = news.UserId, NewsId = newsId };
        return Ok(scope);
    }

    /// <summary>Update news; <c>id</c> required.</summary>
    [HttpPut]
    public async Task<ActionResult> UpdateNews(
        [FromBody] News news,
        [FromServices] IValidator<News> validator,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetCurrentUserId(out var currentUserId))
            return this.UnauthorizedProblem("Missing or invalid user identity in token.");

        if (news.UserId <= 0)
            news.UserId = currentUserId;
        else if (news.UserId != currentUserId)
            return this.ForbiddenProblem("Body userId must match the authenticated user (or omit/zero to use your account).");

        var fv = await validator.ValidateAsync(news, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        await newsService.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>Delete all news rows for this user. No body.</summary>
    [HttpDelete("userId={userId:int}/delete/all")]
    public async Task<ActionResult<object>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        var deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    /// <remarks>
    /// <b>DELETE</b> <c>api/v1/users/news/userId={userId}/newsId={newsId}</c> — no body (same path shape as <see cref="GetNewsById"/>).
    /// </remarks>
    [HttpDelete("userId={userId:int}/newsId={newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        if (this.WhenCannotAccessUser(userId) is { } denied)
            return denied;

        var deleteNews = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (deleteNews is null)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        return Ok();
    }
}
