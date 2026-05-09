namespace Hermes.Application.Models.News;

/// <summary>Response from <c>POST /api/v1/users/news</c>.</summary>
public sealed record CreateNewsResponse(int UserId, int NewsId);
