namespace Hermes.Application.Security;

/// <summary>Result of issuing a JWT access token: the compact string and its absolute UTC expiry.</summary>
/// <param name="Token">The serialized JWT to send as <c>Authorization: Bearer …</c>.</param>
/// <param name="ExpiresAtUtc">Matches the token's <c>exp</c> claim (UTC).</param>
public sealed record JwtAccessTokenResult(string Token, DateTimeOffset ExpiresAtUtc);
