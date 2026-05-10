namespace Hermes.Application.Models.News;

/// <summary>One page of news rows plus pagination metadata (offset and/or cursor).</summary>
public sealed record NewsListResult(
    IReadOnlyList<Domain.Entities.News> Items,
    int Page,
    int PageSize,
    int? TotalCount,
    int? TotalPages,
    bool HasNextPage,
    int? NextAfterId);
