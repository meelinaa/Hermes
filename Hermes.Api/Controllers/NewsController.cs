using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers;

[ApiController]
[Route("api/v1/users/{userId:int}/news")]
public class NewsController(INewsService newsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<News>>> GetList(int userId, CancellationToken cancellationToken)
    {
        var list = await newsService.GetAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpGet("{newsId:int}")]
    public async Task<ActionResult<News>> GetById(int userId, int newsId, CancellationToken cancellationToken)
    {
        var news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        return news is null ? NotFound() : Ok(news);
    }

    [HttpPost]
    public async Task<ActionResult<News>> Post(int userId, [FromBody] News news, CancellationToken cancellationToken)
    {
        if (news.UserId != 0 && news.UserId != userId)
            return BadRequest("News.UserId must match the route or be 0.");
        news.UserId = userId;

        await newsService.SetNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return Ok(news);
    }

    [HttpPut]
    public async Task<ActionResult> Put(int userId, [FromBody] News news, CancellationToken cancellationToken)
    {
        if (news.Id <= 0)
            return BadRequest("News Id is required for update.");
        if (news.UserId != 0 && news.UserId != userId)
            return BadRequest("News.UserId must match the route.");
        news.UserId = userId;

        await newsService.UpdateNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Deletes every news row for this user.</summary>
    [HttpDelete]
    public async Task<ActionResult<object>> DeleteAll(int userId, CancellationToken cancellationToken)
    {
        var deleted = await newsService.DeleteAllNewsByUserAsync(userId, cancellationToken).ConfigureAwait(false);
        return Ok(new { deleted });
    }

    [HttpDelete("{newsId:int}")]
    public async Task<ActionResult> Delete(int userId, int newsId, CancellationToken cancellationToken)
    {
        var news = await newsService.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (news is null)
            return NotFound();

        await newsService.DeleteNewsAsync(news, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
