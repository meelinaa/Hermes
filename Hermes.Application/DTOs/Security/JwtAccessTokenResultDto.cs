namespace Hermes.Application.DTOs.Security;

public sealed record JwtAccessTokenResultDto(string Token, DateTimeOffset ExpiresAtUtc);
