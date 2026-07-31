namespace Hermes.Application.Security;

public sealed record JwtAccessTokenResultDto(string Token, DateTimeOffset ExpiresAtUtc);
