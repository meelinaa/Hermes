using Hermes.Application.Services;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

/// <summary>News under <c>api/v1/users/{userId}/news</c>. <c>userId</c> in route overrides body when needed.</summary>
[ApiController]
[Route("api/v1/users/news")]
public class NewsController(INewsService newsService) : ControllerBase
{
    /// <remarks><b>GET</b> <c>api/v1/users/news/get/list</c> — no body.</remarks>
    [HttpGet("list")]
    public async Task<ActionResult<List<News>>> GetNewsList([FromBody] NewsScope scope, CancellationToken cancellationToken) // TODO: Prepare Body Model for api requests instead of using News directly, to avoid confusion and potential issues with properties like Id.
    {
        var list = await newsService.GetAllNewsByUserAsync(scope.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    /// <remarks><b>GET</b> <c>api/v1/users/get/news/</c> — no body.</remarks>
    [HttpGet]
    public async Task<ActionResult<News>> GetNewsByUserId([FromBody] NewsScope scope, CancellationToken cancellationToken) // TODO: Prepare Body Model for api requests instead of using News directly, to avoid confusion and potential issues with properties like Id.
    {
        var news = await newsService.GetNewsByIdAsync(scope.UserId, scope.NewsId, cancellationToken).ConfigureAwait(false);
        return news is null ? NotFound() : Ok(news);
    }

    /// <summary>Create news for <paramref name="userId"/>.</summary>
    /// <remarks>
    /// <b>POST</b> <c>api/v1/users/{userId}/news</c> — Body:
    /// <code>
    /// {
    ///   "userId": 0,
    ///   "keywords": ["ai", "climate"],
    ///   "category": ["Business", "Science"],
    ///   "languages": ["English", "German"],
    ///   "countries": ["Germany", "Austria"],
    ///   "sendOnWeekdays": ["Monday", "Wednesday"],
    ///   "sendAtTimes": ["08:00:00", "18:30:00"]
    /// }
    /// </code>
    /// Enums: use JSON strings if <c>JsonStringEnumConverter</c> is configured; otherwise numeric enum values.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult> SetNews([FromBody] News news, CancellationToken cancellationToken)
    {
        if (news.UserId != 0)
            return BadRequest("News.UserId must be over 0.");
        //todo: check if user exists before creating news, to avoid orphan news entries. This requires IUserService or similar to check user existence.
        int newsId = await newsService.SetNewsAsync(news, cancellationToken).ConfigureAwait(false); // todo: consider returning created news with Id, or at least the Id in response for better client handling. 
        NewsScope scope = new() { UserId = news.UserId, NewsId = newsId };
        return Ok();
    }

    /// <summary>Update news; <c>id</c> required.</summary>
    /// <remarks>
    /// <b>PUT</b> <c>api/v1/users/{userId}/news</c> — Body (example):
    /// <code>
    /// {
    ///   "id": 5,
    ///   "userId": 0,
    ///   "keywords": ["ai"],
    ///   "category": ["Technology"],
    ///   "languages": ["English"],
    ///   "countries": ["Germany"],
    ///   "sendOnWeekdays": ["Friday"],
    ///   "sendAtTimes": ["09:00:00"]
    /// }
    /// </code>
    /// </remarks>
    [HttpPut]
    public async Task<ActionResult> UpdateNews([FromBody] News news, CancellationToken cancellationToken)
    {
        // todo: check if news exists before updating, to avoid creating new entry if Id is missing or invalid. This requires a GetNewsByIdAsync call first.
        // And also check if news.UserId matches route userId or is 0, to avoid mismatches. This requires userId in route, which currently is not in the route template for this action.

        await newsService.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        NewsScope scope = new() { UserId = news.UserId, NewsId = news.Id };
        return Ok();
    }

    /// <summary>Delete all news rows for this user. No body.</summary>
    /// <remarks><b>DELETE</b> <c>api/v1/users/news</c></remarks>
    [HttpDelete("delete/all")]
    public async Task<ActionResult<object>> DeleteAllNews([FromBody] NewsScope scope, CancellationToken cancellationToken)
    {
        var deleted = await newsService.DeleteAllNewsByUserAsync(scope.UserId, cancellationToken).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    /// <remarks><b>DELETE</b> <c>api/v1/users/news/</c> — no body.</remarks>
    [HttpDelete]
    public async Task<ActionResult> DeleteNews([FromBody]NewsScope scope, CancellationToken cancellationToken)
    {
        var deleteNews = await newsService.GetNewsByIdAsync(scope.UserId, scope.NewsId, cancellationToken).ConfigureAwait(false);
        if (deleteNews is null)
            return NotFound();

        await newsService.DeleteNewsAsync(deleteNews, cancellationToken).ConfigureAwait(false);
        return Ok();
    }
}
