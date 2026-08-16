using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Builders;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.NewsDataIo;

/// <summary>
/// Contains comprehensive unit tests for <see cref="NewsDataIoUrlUtility"/>,
/// verifying dynamic endpoint selection (/v2/top-headlines vs /v2/everything),
/// category normalization, query parameter escaping, and boundary conditions.
/// </summary>
public sealed class NewsDataIoUrlBuilderTests
{
    /// <summary>
    /// Tests that <see cref="NewsDataIoUrlUtility.Build"/> throws <see cref="ArgumentNullException"/>
    /// when the parts DTO is null.
    /// </summary>
    [Fact]
    public void Build_Should_ThrowArgumentNullException_WhenPartsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => NewsDataIoUrlUtility.Build(null!));
    }

    /// <summary>
    /// Tests that <see cref="NewsDataIoUrlUtility.Build"/> throws <see cref="ArgumentException"/>
    /// when the API key is missing, empty, or composed exclusively of whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Build_Should_ThrowArgumentException_WhenApiKeyMissingOrWhitespace(string? invalidApiKey)
    {
        // Arrange
        ApiUrlPartsDto parts = new() { ApiKey = invalidApiKey! };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => NewsDataIoUrlUtility.Build(parts));
    }

    /// <summary>
    /// Tests that requests containing country or category parameters route to the /v2/top-headlines endpoint
    /// and correctly escape query parameters.
    /// </summary>
    [Fact]
    public void Build_Should_RouteToTopHeadlines_WhenCountryAndCategorySpecified()
    {
        // Arrange
        ApiUrlPartsDto parts = new()
        {
            ApiKey = "secret-api-key",
            Countries = ["DE", "US"],
            Categories = ["technology"],
            Q = "AI & Machine Learning"
        };

        // Act
        string url = NewsDataIoUrlUtility.Build(parts);

        // Assert
        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=secret-api-key", url, StringComparison.Ordinal);
        Assert.Contains("country=de", url, StringComparison.Ordinal);
        Assert.Contains("category=technology", url, StringComparison.Ordinal);
        Assert.Contains("q=AI%20%26%20Machine%20Learning", url, StringComparison.Ordinal);
        Assert.Contains("pageSize=30", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that requests with only free-text keywords and languages route to the /v2/everything endpoint.
    /// </summary>
    [Fact]
    public void Build_Should_RouteToEverything_WhenOnlyKeywordsAndLanguageSpecified()
    {
        // Arrange
        ApiUrlPartsDto parts = new()
        {
            ApiKey = "secret-key",
            Languages = ["EN", "DE"],
            Q = "quantum computing"
        };

        // Act
        string url = NewsDataIoUrlUtility.Build(parts);

        // Assert
        Assert.StartsWith("https://newsapi.org/v2/everything?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=secret-key", url, StringComparison.Ordinal);
        Assert.Contains("language=en", url, StringComparison.Ordinal);
        Assert.Contains("q=quantum%20computing", url, StringComparison.Ordinal);
        Assert.Contains("sortBy=publishedAt&pageSize=30", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests all category mappings supported by <see cref="NewsDataIoUrlUtility"/>,
    /// ensuring standard, generic alias, and custom fallback categories are handled.
    /// </summary>
    [Theory]
    [InlineData("business", "business")]
    [InlineData("entertainment", "entertainment")]
    [InlineData("health", "health")]
    [InlineData("science", "science")]
    [InlineData("sports", "sports")]
    [InlineData("technology", "technology")]
    [InlineData("breaking", "general")]
    [InlineData("world", "general")]
    [InlineData("top", "general")]
    [InlineData("politics", "general")]
    [InlineData("environment", "general")]
    [InlineData("food", "general")]
    [InlineData("tourism", "general")]
    [InlineData("custom-topic", "custom-topic")]
    public void Build_Should_NormalizeCategories_ToNewsApiCategories(string inputCategory, string expectedCategory)
    {
        // Arrange
        ApiUrlPartsDto parts = new()
        {
            ApiKey = "test-key",
            Categories = [inputCategory]
        };

        // Act
        string url = NewsDataIoUrlUtility.Build(parts);

        // Assert
        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains($"category={expectedCategory}", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that when no filters are specified, the builder defaults to the /v2/top-headlines endpoint.
    /// </summary>
    [Fact]
    public void Build_Should_RouteToTopHeadlines_WhenNoFiltersProvided()
    {
        // Arrange
        ApiUrlPartsDto parts = new()
        {
            ApiKey = "fallback-key"
        };

        // Act
        string url = NewsDataIoUrlUtility.Build(parts);

        // Assert
        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
        Assert.Contains("apiKey=fallback-key", url, StringComparison.Ordinal);
        Assert.Contains("pageSize=30", url, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that categories with null elements fall back safely without error.
    /// </summary>
    [Fact]
    public void Build_Should_HandleNullCategoryElement()
    {
        // Arrange
        ApiUrlPartsDto parts = new()
        {
            ApiKey = "key",
            Categories = [null!]
        };

        // Act
        string url = NewsDataIoUrlUtility.Build(parts);

        // Assert
        Assert.StartsWith("https://newsapi.org/v2/top-headlines?", url, StringComparison.Ordinal);
    }
}
