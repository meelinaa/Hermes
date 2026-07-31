namespace Hermes.Application.Ports;

/// <summary>
/// Renders verification e-mail content into a deliverable HTML body.
/// Implemented by the Notifications layer to keep HTML templating
/// out of the Application layer (Dependency Inversion).
/// </summary>
public interface IVerificationRenderer
{
    /// <summary>
    /// Produces a complete HTML e-mail body for a verification e-mail
    /// based on the supplied user and code data.
    /// </summary>
    Task<string> RenderVerificationAsync(
        VerificationRenderRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Data needed by the renderer to produce a complete verification HTML body.
/// Defined in Application so the interface stays infrastructure-agnostic.
/// </summary>
public sealed record VerificationRenderRequest(
    string? UserDisplayName,
    string RecipientEmail,
    string VerificationCode,
    string SupportEmail,
    string UnsubscribeUrl,
    string SettingsUrl);
