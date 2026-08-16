using System.Text;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;

/// <summary>
/// Builds query URLs for external News API endpoints (NewsAPI.org /v2/top-headlines and /v2/everything)
/// with intelligent dynamic endpoint selection based on query filters.
/// </summary>
public static class NewsDataIoUrlUtility
{
    private const string BASE_TOP_HEADLINES = "https://newsapi.org/v2/top-headlines?";
    private const string BASE_EVERYTHING = "https://newsapi.org/v2/everything?";

    /// <summary>
    /// Builds a full request URL choosing the optimal endpoint (/v2/top-headlines or /v2/everything)
    /// based on the supplied query parameters.
    /// </summary>
    /// <param name="parts">The structured URL query parts DTO.</param>
    /// <returns>The constructed query URL string.</returns>
    public static string Build(ApiUrlPartsDto parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (string.IsNullOrWhiteSpace(parts.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(parts));

        bool hasCategory = parts.Categories != null && parts.Categories.Any(c => !string.IsNullOrWhiteSpace(c));
        bool hasCountry = parts.Countries != null && parts.Countries.Any(c => !string.IsNullOrWhiteSpace(c));
        bool hasKeywords = !string.IsNullOrWhiteSpace(parts.Q);
        bool hasLanguage = parts.Languages != null && parts.Languages.Any(l => !string.IsNullOrWhiteSpace(l));

        // Use top-headlines if category or country is specified, or if no keyword is present
        if (hasCategory || hasCountry || !hasKeywords)
        {
            StringBuilder sb = new();
            sb.Append(BASE_TOP_HEADLINES);
            sb.Append("apiKey=").Append(Uri.EscapeDataString(parts.ApiKey));

            if (hasCountry && parts.Countries != null)
            {
                string? firstCountry = parts.Countries.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c))?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(firstCountry))
                {
                    sb.Append("&country=").Append(Uri.EscapeDataString(firstCountry));
                }
            }

            if (hasCategory && parts.Categories != null)
            {
                string? rawCategory = parts.Categories.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
                string? mappedCat = MapToNewsApiCategory(rawCategory);
                if (!string.IsNullOrWhiteSpace(mappedCat))
                {
                    sb.Append("&category=").Append(Uri.EscapeDataString(mappedCat));
                }
            }

            if (hasKeywords)
            {
                sb.Append("&q=").Append(Uri.EscapeDataString(parts.Q!));
            }

            sb.Append("&pageSize=30");
            return sb.ToString();
        }
        else
        {
            // Use /v2/everything for free-text search and specific languages
            StringBuilder sb = new();
            sb.Append(BASE_EVERYTHING);
            sb.Append("apiKey=").Append(Uri.EscapeDataString(parts.ApiKey));

            if (hasKeywords)
            {
                sb.Append("&q=").Append(Uri.EscapeDataString(parts.Q!));
            }

            if (hasLanguage && parts.Languages != null)
            {
                string? firstLanguage = parts.Languages.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim().ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(firstLanguage))
                {
                    sb.Append("&language=").Append(Uri.EscapeDataString(firstLanguage));
                }
            }

            sb.Append("&sortBy=publishedAt&pageSize=30");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Maps generic application domain category names to official NewsAPI.org categories.
    /// </summary>
    /// <param name="category">The category string to normalize.</param>
    /// <returns>The official NewsAPI.org category or general.</returns>
    private static string? MapToNewsApiCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        string normalized = category.Trim().ToLowerInvariant();
        return normalized switch
        {
            "business" => "business",
            "entertainment" => "entertainment",
            "health" => "health",
            "science" => "science",
            "sports" => "sports",
            "technology" => "technology",
            "general" or "breaking" or "world" or "top" or "politics" or "environment" or "food" or "tourism" => "general",
            _ => normalized
        };
    }
}
