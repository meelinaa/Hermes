namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Credentials for <c>POST /api/v1/auth/login</c>.</summary>
public sealed class LoginRequestDto
{
    public string NameOrEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
