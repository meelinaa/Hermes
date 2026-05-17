namespace Hermes.Application.Security;

public sealed record JwtAccessTokenResult(string Token, DateTimeOffset ExpiresAtUtc);
