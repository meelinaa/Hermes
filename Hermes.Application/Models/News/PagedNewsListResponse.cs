namespace Hermes.Application.Models.News;

/// <summary>JSON envelope for <c>GET …/news</c>: items and pagination fields.</summary>
public sealed record PagedNewsListResponse(
    List<NewsResponse> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
