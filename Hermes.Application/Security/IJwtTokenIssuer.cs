namespace Hermes.Application.Security;

/// <summary>
/// Application-layer service that creates signed JWT access tokens after the user is authenticated (e.g. password check).
/// Each call produces a new token with a new <c>jti</c>, so two tokens for the same user are still different strings.
/// </summary>
public interface IJwtTokenIssuer
{
    /// <summary>
    /// Creates a JWT containing <c>sub</c>/<c>NameIdentifier</c> (user id), optional email/name, <c>jti</c>, and <c>iat</c>;
    /// signs it with HS256 using <see cref="JwtOptions.SigningKey"/>.
    /// </summary>
    JwtAccessTokenResult Issue(int userId, string? email, string? name);
}
