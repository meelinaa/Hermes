namespace Hermes.Application.Models.Login;

/// <summary>Successful <c>POST /api/v1/auth/refresh</c> payload.</summary>
public sealed record RefreshResponse(
    bool Success,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
