using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.External;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Providers;

/// <summary>
/// HTTP client adapter for retrieving latest news articles from external news APIs (NewsAPI.org &amp; NewsData.io)
/// with robust error handling, daily quota enforcement, API key sanitization, and structured failure logging.
/// </summary>
public sealed class NewsDataIoClient : INewsArticleProvider
{
    private static readonly Regex ApiKeyQueryRegex = new(@"([?&]apiKey=)[^&]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsDataIoClient> _logger;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IOptions<NewsDataIoOptions>? _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsDataIoClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client instance.</param>
    /// <param name="logger">The structured logger.</param>
    /// <param name="redis">Optional Redis connection for daily quota tracking.</param>
    /// <param name="options">Optional NewsDataIo configuration options.</param>
    public NewsDataIoClient(
        HttpClient httpClient,
        ILogger<NewsDataIoClient> logger,
        IConnectionMultiplexer? redis = null,
        IOptions<NewsDataIoOptions>? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _redis = redis;
        _options = options;
    }

    /// <summary>
    /// Fetches latest articles for the supplied query, enforcing daily quota and mapping results into domain models.
    /// </summary>
    /// <param name="query">The news article query criteria.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of fetched news articles.</returns>
    /// <exception cref="DailyQuotaExceededException">Thrown when the daily request budget has been exhausted.</exception>
    /// <exception cref="HttpRequestException">Thrown when the external news provider returns a non-success HTTP status code.</exception>
    public async Task<IReadOnlyList<NewsArticle>> GetLatestAsync(NewsArticleQueryDto query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (_redis is not null && _options?.Value is not null)
        {
            var db = _redis.GetDatabase();
            string todayKey = $"news_provider_daily_usage:{DateTime.UtcNow:yyyy-MM-dd}";
            long currentUsage = await db.StringIncrementAsync(todayKey).ConfigureAwait(false);
            if (currentUsage == 1)
            {
                await db.KeyExpireAsync(todayKey, TimeSpan.FromHours(24)).ConfigureAwait(false);
            }

            if (currentUsage > _options.Value.MaxDailyRequests)
            {
                _logger.LogCritical(
                    "News provider daily quota exceeded ({CurrentUsage}/{MaxDailyRequests}). Short-circuiting request to avoid provider lockout.",
                    currentUsage,
                    _options.Value.MaxDailyRequests);

                throw new DailyQuotaExceededException($"Daily quota of {_options.Value.MaxDailyRequests} requests exceeded for News Provider.");
            }
        }

        ApiUrlPartsDto urlParts = new()
        {
            ApiKey = query.ApiKey,
            Countries = query.Countries,
            Languages = query.Languages,
            Categories = query.Categories,
            Q = query.KeywordsQuery,
            Timezone = query.Timezone,
            Image = query.Image,
            RemoveDuplicate = query.RemoveDuplicate,
            Sort = query.Sort,
            ExcludeField = query.ExcludeField
        };

        string url = NewsDataIoUrlUtility.Build(urlParts);
        string sanitizedUrl = SanitizeUrl(url);

        using HttpRequestMessage requestMessage = new(HttpMethod.Get, url);
        requestMessage.Headers.Add("User-Agent", "Hermes-News-App/1.0");
        if (!string.IsNullOrWhiteSpace(query.ApiKey))
        {
            requestMessage.Headers.Add("X-Api-Key", query.ApiKey);
        }

        HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "News provider API returned error status {StatusCode} ({ReasonPhrase}). Request: {SanitizedUrl}, Body: {ErrorBody}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                sanitizedUrl,
                errorBody);

            throw new HttpRequestException(
                $"News provider API returned error status {(int)response.StatusCode} ({response.ReasonPhrase}): {errorBody}",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        NewsDataIoDto? dto = await JsonSerializer.DeserializeAsync<NewsDataIoDto>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (dto is null)
            return [];

        List<NewsArticle> articles = [];
        foreach (ResultsDto resultItem in dto.AllArticles)
        {
            string? link = resultItem.ResolvedLink;
            if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(resultItem.Title))
                continue;

            List<string>? categories = resultItem.Category;
            if ((categories is null || categories.Count == 0) && resultItem.Source?.Name != null)
            {
                categories = [resultItem.Source.Name];
            }

            articles.Add(new NewsArticle(
                resultItem.ArticleId ?? link,
                link,
                resultItem.Title,
                resultItem.Description,
                categories,
                resultItem.ResolvedImageUrl));
        }

        return articles;
    }

    /// <summary>
    /// Redacts sensitive API key credentials from URL strings before logging or exception generation.
    /// </summary>
    /// <param name="rawUrl">The raw URL potentially containing plain-text API key queries.</param>
    /// <returns>A sanitized URL string with the API key redacted.</returns>
    private static string SanitizeUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return string.Empty;

        return ApiKeyQueryRegex.Replace(rawUrl, "$1***");
    }
}
