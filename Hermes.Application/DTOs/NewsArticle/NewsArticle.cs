namespace Hermes.Application.DTOs.NewsArticle;

/// <summary>
/// Represents a single news article returned from an external news API provider.
/// </summary>
/// <param name="ArticleId">The unique article identifier.</param>
/// <param name="Link">The URL to the original article.</param>
/// <param name="Title">The article headline.</param>
/// <param name="Description">A short description or excerpt of the article.</param>
/// <param name="Category">The list of category tags associated with the article.</param>
/// <param name="ImageUrl">Optional URL of the article's thumbnail image.</param>
public sealed record NewsArticle(
    string? ArticleId,
    string? Link,
    string? Title,
    string? Description,
    IReadOnlyList<string>? Category,
    string? ImageUrl);
