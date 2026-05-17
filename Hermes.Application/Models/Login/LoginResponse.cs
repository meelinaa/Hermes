namespace Hermes.Application.Models.Login;

public sealed record LoginResponse(
    bool Success,
    int UserId,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
