using Hermes.Application.DTOs.Email;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Renders verification e-mail content into a deliverable HTML body.
/// Implemented by the Notifications layer to keep HTML templating
/// out of the Application layer (Dependency Inversion).
/// </summary>
public interface IVerificationHtmlService
{
    /// <summary>
    /// Produces a complete HTML e-mail body for a verification e-mail
    /// based on the supplied user and code data.
    /// </summary>
    Task<string> RenderVerificationAsync(
        VerificationRenderRequest request,
        CancellationToken cancellationToken = default);
}

