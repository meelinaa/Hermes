namespace Hermes.Application.Models.Login;

/// <summary>Body for <c>POST /auth/refresh</c>: the current opaque refresh token (not the JWT).</summary>
public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = "";
}
