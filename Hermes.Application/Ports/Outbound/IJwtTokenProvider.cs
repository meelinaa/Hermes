namespace Hermes.Application.Ports.Outbound;

using Hermes.Application.DTOs.Security;
using Hermes.Application.Security;
public interface IJwtTokenProvider
{
    JwtAccessTokenResultDto Issue(int userId, string? email, string? name);
}
