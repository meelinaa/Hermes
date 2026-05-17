namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>User profile summary from <c>GET /api/v1/users/{id}</c>.</summary>
public sealed class UserScopeDto
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public int UserId { get; set; }

    public bool IsEmailVerified { get; set; }
}
