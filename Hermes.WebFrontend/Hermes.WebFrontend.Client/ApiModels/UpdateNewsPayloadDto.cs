using Hermes.WebFrontend.Client.ApiModels.Enums;

namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>Body for <c>PUT /api/v1/users/news</c>.</summary>
public sealed class UpdateNewsPayloadDto
{
    public int Id { get; set; }

    public List<string>? Keywords { get; set; }

    public List<NewsCategory>? Category { get; set; }

    public List<Language>? Languages { get; set; }

    public List<Country>? Countries { get; set; }

    public List<Weekdays> SendOnWeekdays { get; set; } = [];

    public List<TimeOnly> SendAtTimes { get; set; } = [];

    public bool IsEnabled { get; set; } = true;
}
