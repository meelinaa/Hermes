namespace Hermes.Application.Models.News;

public sealed record NewsListResult(
    IReadOnlyList<Domain.Entities.News> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
