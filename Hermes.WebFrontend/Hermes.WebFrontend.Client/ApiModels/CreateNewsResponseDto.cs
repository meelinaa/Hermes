namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Response from <c>POST /api/v1/users/news</c>.</summary>
public sealed class CreateNewsResponseDto
{
    public int UserId { get; set; }

    public int NewsId { get; set; }
}
