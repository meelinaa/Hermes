using Hermes.Domain.Enums;

namespace Hermes.Application.Models.News;

/// <summary>Parameters for listing news rows with pagination, optional text search, and category filter.</summary>
public sealed record NewsListQuery(
    int UserId,
    int Page,
    int PageSize,
    int? AfterId,
    bool SortDescending,
    string? Search,
    NewsCategory? Category);
