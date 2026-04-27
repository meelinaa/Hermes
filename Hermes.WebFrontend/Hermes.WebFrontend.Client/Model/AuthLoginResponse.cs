namespace Hermes.WebFrontend.Client.Model;

/// <summary>JSON body for successful POST /api/v1/auth/login.</summary>
public sealed class AuthLoginResponse
{
    public bool Success { get; set; }
    public int? UserId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
}
