using Hermes.Domain.Enums;

namespace Hermes.Application.Models.News;

/// <summary>Body for <c>POST /api/v1/users/news</c>; owning user comes from the JWT (no <c>userId</c> in JSON).</summary>
public sealed record CreateNewsRequest
{
    public List<string>? Keywords { get; init; }

    public List<NewsCategory>? Category { get; init; }

    public List<Language>? Languages { get; init; }

    public List<Country>? Countries { get; init; }

    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    public List<TimeOnly> SendAtTimes { get; init; } = [];
}
