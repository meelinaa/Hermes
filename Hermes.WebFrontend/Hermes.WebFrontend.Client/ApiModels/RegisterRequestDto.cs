namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Body for <c>POST /api/v1/users</c> (registration).</summary>
public sealed class RegisterRequestDto
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
