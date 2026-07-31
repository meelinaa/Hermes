namespace Hermes.Application.Security;

public interface IJwtTokenIssuer
{
    JwtAccessTokenResultDto Issue(int userId, string? email, string? name);
}
