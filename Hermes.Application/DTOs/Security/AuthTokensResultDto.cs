namespace Hermes.Application.DTOs.Security;

public sealed record AuthTokensResultDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc);
