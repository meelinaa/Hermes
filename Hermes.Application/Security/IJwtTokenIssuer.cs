namespace Hermes.Application.Security;

public interface IJwtTokenIssuer
{
    JwtAccessTokenResult Issue(int userId, string? email, string? name);
}
