namespace Hermes.Application.Models.Login;

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
