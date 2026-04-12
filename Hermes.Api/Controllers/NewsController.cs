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
    public async Task<ActionResult<List<News>>> GetNewsList(int userId, CancellationToken cancellationToken) // TODO: Prepare Body Model for api requests instead of using News directly, to avoid confusion and potential issues with properties like Id.
    {
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
        var news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? this.NotFoundProblem() : Ok(news);
    }

    /// <summary>Create news for <paramref name="userId"/>.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users/news</c> — Body (<c>userId</c> &gt; 0). Enum fields use underlying integer values (see <see cref="Hermes.Domain.Enums"/>).
    /// <code>
    /// {
    ///   "userId": 1,
    ///   "keywords": ["ai", "climate"],
    ///   "category": [1, 12],
    ///   "languages": [15, 20],
    ///   "countries": [14, 2],
    ///   "sendOnWeekdays": [0, 2],
    ///   "sendAtTimes": ["08:00:00", "18:30:00"]
    /// }
    /// </code>
    /// Mapping: <c>category</c> → <see cref="Hermes.Domain.Enums.NewsCategory"/> (e.g. 1=<c>Business</c>, 12=<c>Science</c>);
    /// <c>languages</c> → <see cref="Hermes.Domain.Enums.Language"/> (15=<c>English</c>, 20=<c>German</c>);
    /// <c>countries</c> → <see cref="Hermes.Domain.Enums.Country"/> (14=<c>Germany</c>, 2=<c>Austria</c>);
    /// <c>sendOnWeekdays</c> → <see cref="Hermes.Domain.Enums.Weekdays"/> (0=<c>Monday</c>, 2=<c>Wednesday</c>).
    /// With <c>JsonStringEnumConverter</c>, you may send the same members as strings instead (e.g. <c>"Business"</c> for <c>NewsCategory</c>).
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<NewsScope>> SetNews([FromBody] News news, CancellationToken cancellationToken)
    {
        int newsId = await newsService.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        NewsScope scope = new() { UserId = news.UserId, NewsId = newsId };
        return Ok(scope);
    }

    /// <summary>Update news; <c>id</c> required.</summary>
    /// <remarks>
    /// <b>PUT</b> <c>api/v1/users/news</c> — Body (same enum rules as POST; <c>id</c> and <c>userId</c> must identify the row):
    /// <code>
    /// {
    ///   "id": 5,
    ///   "userId": 1,
    ///   "keywords": ["ai"],
    ///   "category": [14],
    ///   "languages": [15],
    ///   "countries": [14],
    ///   "sendOnWeekdays": [4],
    ///   "sendAtTimes": ["09:00:00"]
    /// }
    /// </code>
    /// Example: <c>NewsCategory.Technology</c> = 14, <c>Weekdays.Friday</c> = 4, <c>Country.Germany</c> = 14, <c>Language.English</c> = 15.
    /// </remarks>
    [HttpPut]
    public async Task<ActionResult> UpdateNews(
        [FromBody] News news,
        IValidator<News> validator,
        CancellationToken cancellationToken)
    {
        var fv = await validator.ValidateAsync(news, cancellationToken).ConfigureAwait(false);
        if (!fv.IsValid)
            return fv.ToValidationProblem(this);

        // todo: check if news exists before updating, to avoid creating new entry if Id is missing or invalid. This requires a GetNewsByIdAsync call first.
        // And also check if news.UserId matches route userId or is 0, to avoid mismatches. This requires userId in route, which currently is not in the route template for this action.

        await newsService.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        NewsScope scope = new() { UserId = news.UserId, NewsId = news.Id };
        return Ok();
    }

    /// <summary>Delete all news rows for this user. No body.</summary>
    /// <remarks>
    /// <b>DELETE</b> <c>api/v1/users/news/userId={userId}/delete/all</c> — no body (named user id in path, same style as other news routes).
    /// </remarks>
    [HttpDelete("userId={userId:int}/delete/all")]
    public async Task<ActionResult<object>> DeleteAllNews(int userId, CancellationToken cancellationToken)
    {
        var deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    /// <remarks>
    /// <b>DELETE</b> <c>api/v1/users/news/userId={userId}/newsId={newsId}</c> — no body (same path shape as <see cref="GetNewsById"/>).
    /// </remarks>
    [HttpDelete("userId={userId:int}/newsId={newsId:int}")]
    public async Task<ActionResult> DeleteNews(int userId, int newsId, CancellationToken cancellationToken)
    {
        var deleteNews = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (deleteNews is null)
            return this.NotFoundProblem();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        return Ok();
    }
}
