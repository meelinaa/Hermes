namespace Hermes.Application.Models.Login;

/// <summary>Successful <c>POST /api/v1/auth/login</c> payload.</summary>
public sealed record LoginResponse(
    bool Success,
    int UserId,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
