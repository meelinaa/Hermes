namespace Hermes.Application.Security;

/// <summary>
/// Pair returned to the client after login or refresh: short-lived JWT plus long-lived opaque refresh token.
/// </summary>
/// <param name="AccessToken">JWT for <c>Authorization: Bearer</c> on API calls.</param>
/// <param name="AccessTokenExpiresAtUtc">When the access JWT expires.</param>
/// <param name="RefreshToken">Opaque secret; send only to <c>POST /auth/refresh</c> (or store securely).</param>
/// <param name="RefreshTokenExpiresAtUtc">When the refresh row expires if not rotated sooner.</param>
public sealed record AuthTokensResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
