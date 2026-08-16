using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Options.External;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="ArticleFetchingService"/>,
/// testing external API query constructions, fallback news catalog filtering,
/// and multi-criteria query matching across languages, countries, categories, and keywords.
/// </summary>
public sealed class ArticleFetchingServiceTests
{
    private static ArticleFetchingService CreateSut(
        INewsArticleProvider? provider = null,
        string? apiKey = "valid-api-key")
    {
        IOptions<NewsDataIoOptions> options = Options.Create(new NewsDataIoOptions
        {
            Key = apiKey
        });
        return new ArticleFetchingService(provider ?? Mock.Of<INewsArticleProvider>(), options);
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchArticlesForSubscriptionAsync"/> throws an <see cref="InvalidOperationException"/>
    /// when the NewsData.io API key is null or whitespace.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FetchArticlesForSubscriptionAsync_Should_ThrowInvalidOperationException_WhenApiKeyMissing(string? emptyKey)
    {
        // Arrange
        ArticleFetchingService sut = CreateSut(apiKey: emptyKey);
        NewsletterSubscription subscription = NewsletterSubscription.CreateForUser(new UserId(1));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.FetchArticlesForSubscriptionAsync(subscription));
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchArticlesForSubscriptionAsync"/> returns an empty collection
    /// without querying the provider when the subscription has no filters configured.
    /// </summary>
    [Fact]
    public async Task FetchArticlesForSubscriptionAsync_Should_ReturnEmpty_WhenSubscriptionHasNoFilters()
    {
        // Arrange
        Mock<INewsArticleProvider> provider = new();
        ArticleFetchingService sut = CreateSut(provider.Object);
        NewsletterSubscription subscription = NewsletterSubscription.CreateForUser(new UserId(1));
        subscription.UpdateFilters(null, null, null, null);

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchArticlesForSubscriptionAsync(subscription);

        // Assert
        Assert.Empty(articles);
        provider.Verify(p => p.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchArticlesForSubscriptionAsync"/> constructs the query DTO
    /// with mapped ISO country codes, language codes, category strings, and OR-joined keywords.
    /// </summary>
    [Fact]
    public async Task FetchArticlesForSubscriptionAsync_Should_BuildQueryAndDelegateToProvider()
    {
        // Arrange
        Mock<INewsArticleProvider> provider = new();
        NewsArticleQueryDto? capturedQuery = null;
        provider.Setup(p => p.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<NewsArticleQueryDto, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync([new NewsArticle("art-1", "https://link", "Title", "Desc", ["technology"], "https://img")]);

        ArticleFetchingService sut = CreateSut(provider.Object);
        NewsletterSubscription subscription = NewsletterSubscription.CreateForUser(new UserId(1));
        subscription.UpdateFilters(
            keywords: ["ai", "dotnet"],
            categories: [NewsCategory.Technology],
            languages: [Language.German, Language.English],
            countries: [Country.Germany, Country.USA]);

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchArticlesForSubscriptionAsync(subscription);

        // Assert
        Assert.Single(articles);
        Assert.NotNull(capturedQuery);
        Assert.Equal("valid-api-key", capturedQuery!.ApiKey);
        Assert.Equal(["de", "us"], capturedQuery.Countries);
        Assert.Equal(["de", "en"], capturedQuery.Languages);
        Assert.Equal(["technology"], capturedQuery.Categories);
        Assert.Equal("ai OR dotnet", capturedQuery.KeywordsQuery);
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchPreviewArticlesAsync"/> throws <see cref="ArgumentNullException"/>
    /// when the request is null.
    /// </summary>
    [Fact]
    public async Task FetchPreviewArticlesAsync_Should_ThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        ArticleFetchingService sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.FetchPreviewArticlesAsync(null!));
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchPreviewArticlesAsync"/> returns filtered fallback articles
    /// when the API key is not configured.
    /// </summary>
    [Fact]
    public async Task FetchPreviewArticlesAsync_Should_UseFallback_WhenApiKeyMissing()
    {
        // Arrange
        ArticleFetchingService sut = CreateSut(apiKey: null);
        NewsPreviewRequestDto request = new()
        {
            Categories = [NewsCategory.Technology],
            Languages = [Language.German]
        };

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchPreviewArticlesAsync(request);

        // Assert
        Assert.NotEmpty(articles);
        Assert.All(articles, a => Assert.Contains("technology", a.Category!));
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchPreviewArticlesAsync"/> falls back to the static fallback catalog
    /// when the external news provider throws an exception.
    /// </summary>
    [Fact]
    public async Task FetchPreviewArticlesAsync_Should_Fallback_WhenProviderThrows()
    {
        // Arrange
        Mock<INewsArticleProvider> provider = new();
        provider.Setup(p => p.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Provider offline"));

        ArticleFetchingService sut = CreateSut(provider.Object);
        NewsPreviewRequestDto request = new()
        {
            Categories = [NewsCategory.Business],
            Languages = [Language.English]
        };

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchPreviewArticlesAsync(request);

        // Assert
        Assert.NotEmpty(articles);
        Assert.All(articles, a => Assert.Contains("business", a.Category!));
    }

    /// <summary>
    /// Tests that <see cref="ArticleFetchingService.FetchPreviewArticlesAsync"/> returns provider results
    /// when the external API succeeds and returns articles.
    /// </summary>
    [Fact]
    public async Task FetchPreviewArticlesAsync_Should_ReturnProviderArticles_WhenApiSucceeds()
    {
        // Arrange
        Mock<INewsArticleProvider> provider = new();
        provider.Setup(p => p.GetLatestAsync(It.IsAny<NewsArticleQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new NewsArticle("live-1", "https://news", "Live Title", "Live Desc", ["sports"], null)]);

        ArticleFetchingService sut = CreateSut(provider.Object);
        NewsPreviewRequestDto request = new()
        {
            Categories = [NewsCategory.Sports],
            Keywords = "football"
        };

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchPreviewArticlesAsync(request);

        // Assert
        Assert.Single(articles);
        Assert.Equal("live-1", articles[0].ArticleId);
        Assert.Equal("Live Title", articles[0].Title);
    }

    /// <summary>
    /// Tests keyword filtering in the fallback catalog matching keywords in either title or description.
    /// </summary>
    [Fact]
    public async Task FetchPreviewArticlesAsync_Should_FilterFallbackPoolByKeywords()
    {
        // Arrange
        ArticleFetchingService sut = CreateSut(apiKey: null);
        NewsPreviewRequestDto request = new()
        {
            Keywords = "WebAssembly"
        };

        // Act
        IReadOnlyList<NewsArticle> articles = await sut.FetchPreviewArticlesAsync(request);

        // Assert
        Assert.NotEmpty(articles);
        Assert.All(articles, a =>
            Assert.True(
                (a.Title != null && a.Title.Contains("WebAssembly", StringComparison.OrdinalIgnoreCase)) ||
                (a.Description != null && a.Description.Contains("WebAssembly", StringComparison.OrdinalIgnoreCase))));
    }
}
