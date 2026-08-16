using Hermes.Application.DTOs.Security;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for creating signed JWT access tokens containing standard user claims and expiration timestamps.
/// </summary>
public interface IJwtTokenProvider
{
    /// <summary>
    /// Issues a signed JWT access token for the given user identity.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="email">Optional email address claim.</param>
    /// <param name="name">Optional user display name claim.</param>
    /// <returns>A <see cref="JwtAccessTokenResultDto"/> containing the token string and expiration timestamp.</returns>
    JwtAccessTokenResultDto Issue(UserId userId, string? email, string? name);
}
