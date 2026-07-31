namespace Hermes.Application.DTOs.Login;

public sealed record RefreshResponse(
    bool Success,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
