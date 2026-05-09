namespace Hermes.Application.Models.News;

/// <summary>Response for bulk delete of all news rows for a user.</summary>
public sealed record DeleteAllNewsResponse(int Deleted);
