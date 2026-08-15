using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.NewsDataIo;

public sealed class NewsDataIoUrlBuilderTests
{
    [Fact]
    public void Build_ThrowsArgumentNull_WhenPartsNull() => Assert.Throws<ArgumentNullException>(() => NewsDataIoUrlUtility.Build(null!));

    [Fact]
    public void Build_Throws_WhenApiKeyMissing()
    {
        Assert.Throws<ArgumentException>(() =>
            NewsDataIoUrlUtility.Build(new ApiUrlPartsDto { ApiKey = "" }));

        Assert.Throws<ArgumentException>(() =>
            NewsDataIoUrlUtility.Build(new ApiUrlPartsDto { ApiKey = "   " }));
    }

    [Fact]
    public void Build_RoutesToTopHeadlines_WhenCountryAndCategorySpecified()
    {
        string url = NewsDataIoUrlUtility.Build(new ApiUrlPartsDto
        {
            ApiKey = "test-key",
            Countries = ["de"],
            Categories = ["technology"],
            Q = "AI"
        });

        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=test-key", url, StringComparison.Ordinal);
        Assert.Contains("country=de", url, StringComparison.Ordinal);
        Assert.Contains("category=technology", url, StringComparison.Ordinal);
        Assert.Contains("q=AI", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RoutesToEverything_WhenOnlyKeywordsAndLanguageSpecified()
    {
        string url = NewsDataIoUrlUtility.Build(new ApiUrlPartsDto
        {
            ApiKey = "test-key",
            Languages = ["en"],
            Q = "climate change"
        });

        Assert.StartsWith("https://newsapi.org/v2/everything?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=test-key", url, StringComparison.Ordinal);
        Assert.Contains("language=en", url, StringComparison.Ordinal);
        Assert.Contains("q=climate%20change", url, StringComparison.Ordinal);
        Assert.Contains("sortBy=publishedAt", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MapsDomainCategoriesToNewsApiCategories()
    {
        string url = NewsDataIoUrlUtility.Build(new ApiUrlPartsDto
        {
            ApiKey = "test-key",
            Categories = ["breaking"]
        });

        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains("category=general", url, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_RoutesToTopHeadlines_WhenNoFiltersProvided()
    {
        string url = NewsDataIoUrlUtility.Build(new ApiUrlPartsDto
        {
            ApiKey = "test-key"
        });

        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=test-key", url, StringComparison.Ordinal);
    }
}
