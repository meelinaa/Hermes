namespace Hermes.Application.Security;

public interface IJwtTokenProvider
{
    JwtAccessTokenResultDto Issue(int userId, string? email, string? name);
}
