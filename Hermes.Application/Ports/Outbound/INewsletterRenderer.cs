namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Renders newsletter digest content into a deliverable HTML body.
/// Implemented by the Notifications layer to keep HTML templating
/// out of the Application layer (Dependency Inversion).
/// </summary>
public interface INewsletterRenderer
{
    /// <summary>
    /// Produces a complete HTML e-mail body for a newsletter digest
    /// based on the supplied user context and article list.
    /// </summary>
    Task<string> RenderNewsletterAsync(
        NewsletterRenderRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data needed by the renderer to produce a complete newsletter HTML body.
/// Defined in Application so the interface stays infrastructure-agnostic.
/// </summary>
public sealed record NewsletterRenderRequestDto(
    string? UserDisplayName,
    IReadOnlyList<NewsletterArticleItemDto> Articles);

/// <summary>Single article to be rendered inside a newsletter digest.</summary>
public sealed record NewsletterArticleItemDto(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);
