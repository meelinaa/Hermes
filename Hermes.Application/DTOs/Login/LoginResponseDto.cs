namespace Hermes.Application.DTOs.Login;

public sealed record LoginResponseDto(
    bool Success,
    int UserId,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
