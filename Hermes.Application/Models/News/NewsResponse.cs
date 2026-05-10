using Hermes.Domain.Enums;

namespace Hermes.Application.Models.News;

public sealed record NewsResponse
{
    public int Id { get; init; }

    public int UserId { get; init; }

    public List<string>? Keywords { get; init; }

    public List<NewsCategory>? Category { get; init; }

    public List<Language>? Languages { get; init; }

    public List<Country>? Countries { get; init; }

    public List<Weekdays> SendOnWeekdays { get; init; } = [];

    public List<TimeOnly> SendAtTimes { get; init; } = [];
}
