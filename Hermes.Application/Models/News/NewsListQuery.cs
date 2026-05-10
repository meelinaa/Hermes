using Hermes.Domain.Enums;

namespace Hermes.Application.Models.News;

public sealed record NewsListQuery(
    int UserId,
    int Page,
    int PageSize,
    int? AfterId,
    bool SortDescending,
    string? Search,
    NewsCategory? Category);
