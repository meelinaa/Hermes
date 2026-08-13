using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Options.External;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

public sealed class ArticleFetchingService(
    INewsArticleProvider newsArticleProvider,
    IOptions<NewsDataIoOptions> newsDataOptions) : IArticleFetchingService
{
    public async Task<IReadOnlyList<NewsArticle>> FetchArticlesForSubscriptionAsync(NewsletterSubscription subscription, CancellationToken cancellationToken = default)
    {
        string? apiKey = newsDataOptions.Value.Key?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure NewsDataIo:Key.");

        NewsArticleQueryDto? query = BuildArticleQuery(apiKey, subscription);
        if (query is null)
            return Array.Empty<NewsArticle>();

        return await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
    }

    private static NewsArticleQueryDto? BuildArticleQuery(string apiKey, NewsletterSubscription subscription)
    {
        List<string>? countries = subscription.Countries is { Count: > 0 }
            ? subscription.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = subscription.Languages is { Count: > 0 }
            ? subscription.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = subscription.Category is { Count: > 0 }
            ? subscription.Category.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (subscription.Keywords is { Count: > 0 })
        {
            List<string> terms = subscription.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).ToList();
            if (terms.Count > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        if (countries is null && languages is null && categories is null && string.IsNullOrWhiteSpace(keywordsQuery))
            return null;

        return new NewsArticleQueryDto
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };
    }
}
