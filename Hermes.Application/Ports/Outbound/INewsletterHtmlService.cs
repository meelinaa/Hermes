using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Renders newsletter digest content into a deliverable HTML body.
/// Implemented by the Notifications layer to keep HTML templating
/// out of the Application layer (Dependency Inversion).
/// </summary>
public interface INewsletterHtmlService
{
    /// <summary>
    /// Produces a complete HTML e-mail body for a newsletter digest
    /// based on the supplied user context and article list.
    /// </summary>
    Task<string> RenderNewsletterAsync(
        NewsletterRenderRequestDto request,
        CancellationToken cancellationToken = default);
}

