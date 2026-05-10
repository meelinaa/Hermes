using Hermes.Domain.Entities;

namespace Hermes.WebFrontend.Client.Services.NewsService;

/// <summary>Matches the paged news list JSON from the API (camelCase properties).</summary>
public sealed class NewsListPageDto
{
    public List<News> Items { get; set; } = [];

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int? TotalCount { get; set; }

    public int? TotalPages { get; set; }

    public bool HasNextPage { get; set; }

    public int? NextAfterId { get; set; }
}
