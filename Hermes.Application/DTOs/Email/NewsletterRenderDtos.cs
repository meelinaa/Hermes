namespace Hermes.Application.DTOs.Email;

/// <summary>
/// Data needed by the renderer to produce a complete newsletter HTML body.
/// Defined in Application so the interface stays infrastructure-agnostic.
/// </summary>
/// <param name="UserDisplayName">Optional display name of the newsletter recipient.</param>
/// <param name="Articles">The ordered list of article items to render inside the digest.</param>
public sealed record NewsletterRenderRequestDto(
    string? UserDisplayName,
    IReadOnlyList<NewsletterArticleItemDto> Articles);

/// <summary>
/// Represents a single article to be rendered inside a newsletter digest e-mail.
/// </summary>
/// <param name="Category">The article's category label.</param>
/// <param name="Title">The article headline.</param>
/// <param name="Content">A short excerpt or summary of the article.</param>
/// <param name="Url">The URL to the full article.</param>
/// <param name="ImageUrl">The URL of the article's thumbnail image.</param>
public sealed record NewsletterArticleItemDto(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);
