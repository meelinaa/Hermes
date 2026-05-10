using Hermes.WebFrontend.Client.ApiModels.Enums;

namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>News configuration row (list/detail JSON from the API).</summary>
public sealed class NewsSubscriptionDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public List<string>? Keywords { get; set; }

    public List<NewsCategory>? Category { get; set; }

    public List<Language>? Languages { get; set; }

    public List<Country>? Countries { get; set; }

    public List<Weekdays> SendOnWeekdays { get; set; } = [];

    public List<TimeOnly> SendAtTimes { get; set; } = [];
}
