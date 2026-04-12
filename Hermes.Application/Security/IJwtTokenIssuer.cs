namespace Hermes.Application.Security;

/// <summary>Creates signed access tokens for authenticated users (unique payload per user and issuance time).</summary>
public interface IJwtTokenIssuer
{
    /// <summary>Issues a bearer token with user-specific claims and a new <c>jti</c> per call.</summary>
    JwtAccessTokenResult Issue(int userId, string? email, string? name);
}
