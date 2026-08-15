using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Services.Newsletter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.Api.Controllers.Newsletter;

/// <summary>
/// Exposes endpoints for real-time news feed queries and previewing newsletter digest articles.
/// </summary>
[Authorize]
[ApiController]
[Route("api/v1/news")]
public class NewsFeedController(IArticleFetchingService articleFetchingService) : ControllerBase
{
    /// <summary>
    /// Fetches live news articles matching the supplied search criteria for real-time feed exploration.
    /// </summary>
    /// <param name="request">The news preview filter criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of matching news articles.</returns>
    [HttpPost("preview")]
    public async Task<ActionResult<IReadOnlyList<NewsArticle>>> GetNewsPreview(
        [FromBody] NewsPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<NewsArticle> articles = await articleFetchingService
            .FetchPreviewArticlesAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return Ok(articles);
    }
}
