using Hermes.Application.DTOs.User;

namespace Hermes.Api.Mapping.Users;

/// <summary>
/// Static mapper extension class converting user scope DTOs to HTTP response DTOs.
/// </summary>
internal static class UserHttpMapper
{
    /// <summary>
    /// Converts a <see cref="UserScopeDto"/> instance into a <see cref="UserResponseDto"/> HTTP response payload.
    /// </summary>
    /// <param name="scope">The user scope DTO containing core user profile details.</param>
    /// <returns>The mapped <see cref="UserResponseDto"/>.</returns>
    public static UserResponseDto ToUserResponse(this UserScopeDto scope) =>
        new()
        {
            UserId = scope.UserId,
            Name = scope.Name,
            Email = scope.Email,
            IsEmailVerified = scope.IsEmailVerified,
        };
}
