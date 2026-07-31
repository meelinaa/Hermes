using Hermes.Application.DTOs;
using Hermes.Application.Models.User;

namespace Hermes.Api.Mapping;

internal static class UserHttpMapper
{
    public static UserResponse ToUserResponse(this UserScope scope) =>
        new()
        {
            UserId = scope.UserId,
            Name = scope.Name,
            Email = scope.Email,
            IsEmailVerified = scope.IsEmailVerified,
        };
}
