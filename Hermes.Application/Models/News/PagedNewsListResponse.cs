namespace Hermes.Application.Models.News;

public sealed record PagedNewsListResponse(
    List<NewsResponse> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
