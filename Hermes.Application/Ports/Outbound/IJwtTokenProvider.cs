namespace Hermes.Application.Ports.Outbound;

using Hermes.Application.DTOs.Security;
using Hermes.Application.Services.Security;
using Hermes.Domain.ValueObjects;
public interface IJwtTokenProvider
{
    JwtAccessTokenResultDto Issue(UserId userId, string? email, string? name);
}
