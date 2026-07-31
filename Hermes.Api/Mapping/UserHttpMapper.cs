using Hermes.Application.DTOs.User;

namespace Hermes.Api.Mapping;

internal static class UserHttpMapper
{
    public static UserResponseDto ToUserResponse(this UserScopeDto scope) =>
        new()
        {
            UserId = scope.UserId,
            Name = scope.Name,
            Email = scope.Email,
            IsEmailVerified = scope.IsEmailVerified,
        };
}
