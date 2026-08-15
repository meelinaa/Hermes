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
using Hermes.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service implementation for fetching live articles from news data providers with fallback support.
/// </summary>
public sealed class ArticleFetchingService(
    INewsArticleProvider newsArticleProvider,
    IOptions<NewsDataIoOptions> newsDataOptions) : IArticleFetchingService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<NewsArticle>> FetchPreviewArticlesAsync(NewsPreviewRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? apiKey = newsDataOptions.Value.Key?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
            return GetFallbackPreviewArticles(request);

        List<string>? countries = request.Countries is { Count: > 0 }
            ? request.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = request.Languages is { Count: > 0 }
            ? request.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = request.Categories is { Count: > 0 }
            ? request.Categories.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            string[] terms = request.Keywords.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (terms.Length > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        NewsArticleQueryDto query = new()
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };

        try
        {
            IReadOnlyList<NewsArticle> articles = await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
            return articles.Count > 0 ? articles : GetFallbackPreviewArticles(request);
        }
        catch
        {
            return GetFallbackPreviewArticles(request);
        }
    }

    private static IReadOnlyList<NewsArticle> GetFallbackPreviewArticles(NewsPreviewRequestDto request)
    {
        List<NewsArticle> pool =
        [
            new(
                "sample-tech-1",
                "https://www.heise.de",
                "Künstliche Intelligenz und Quantencomputing: Die nächste Welle in der Softwarearchitektur",
                "Neueste Entwicklungen bei generativen KI-Modellen und autonomen Agentensystemen verändern die Arbeitsweise von Entwicklerteams nachhaltig.",
                ["technology"],
                "https://images.unsplash.com/photo-1518770660439-4636190af475?w=600&auto=format&fit=crop&q=80"
            ),
            new(
                "sample-biz-2",
                "https://www.handelsblatt.com",
                "Europäische Tech-Startups verzeichnen starkes Wachstum bei nachhaltigen Technologien",
                "Investitionen in grüne Rechenzentren und energieeffiziente Cloud-Infrastruktur erreichen neue Höchststände im ersten Quartal.",
                ["business"],
                "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=600&auto=format&fit=crop&q=80"
            ),
            new(
                "sample-sci-3",
                "https://www.spektrum.de",
                "Neue Entdeckungen in der Raumfahrt: James-Webb-Teleskop findet Spuren ferner Atmosphären",
                "Astronomen analysieren hochauflösende Spektraldaten neu entdeckter Exoplaneten mit bislang unerreichter Präzision.",
                ["science"],
                "https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=600&auto=format&fit=crop&q=80"
            ),
            new(
                "sample-world-4",
                "https://www.tagesschau.de",
                "Internationale Energiekonferenz beschließt neue Standards für digitale Netze",
                "Vertreter aus über 40 Nationen verständigen sich auf einheitliche Richtlinien zur Resilienz kritischer digitaler Netzinfrastrukturen.",
                ["world"],
                "https://images.unsplash.com/photo-1509391365360-2e959784a276?w=600&auto=format&fit=crop&q=80"
            ),
            new(
                "sample-tech-5",
                "https://www.golem.de",
                ".NET 10 und WebAssembly: Höhere Performance für moderne Webanwendungen",
                "Mit Ahead-of-Time-Kompilierung und optimiertem Speichermanagement setzen moderne WebAssembly-Frameworks neue Maßstäbe bei Reaktionszeiten.",
                ["technology"],
                "https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=600&auto=format&fit=crop&q=80"
            )
        ];

        // Filter based on requested categories if specified
        if (request.Categories is { Count: > 0 })
        {
            var catNames = request.Categories.Select(c => c.ToString().ToLowerInvariant()).ToHashSet();
            var filtered = pool.Where(a => a.Category != null && a.Category.Any(c => catNames.Contains(c.ToLowerInvariant()))).ToList();
            if (filtered.Count > 0)
                return filtered;
        }

        // Filter based on keywords if specified
        if (!string.IsNullOrWhiteSpace(request.Keywords))
        {
            string kw = request.Keywords.Trim().ToLowerInvariant();
            var filtered = pool.Where(a => (a.Title != null && a.Title.ToLowerInvariant().Contains(kw)) ||
                                           (a.Description != null && a.Description.ToLowerInvariant().Contains(kw))).ToList();
            if (filtered.Count > 0)
                return filtered;
        }

        return pool;
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
