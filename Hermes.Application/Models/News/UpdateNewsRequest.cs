using Hermes.Domain.Enums;

namespace Hermes.Application.Models.News;

/// <summary>Body for <c>PUT /api/v1/users/news</c>; <see cref="Id"/> identifies the row; owner is the authenticated user.</summary>
public sealed record UpdateNewsRequest
{
    public int Id { get; init; }

    public List<string>? Keywords { get; init; }

    public List<NewsCategory>? Category { get; init; }

    public List<Language>? Languages { get; init; }

    public List<Country>? Countries { get; init; }

    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    public List<TimeOnly> SendAtTimes { get; init; } = [];
}
