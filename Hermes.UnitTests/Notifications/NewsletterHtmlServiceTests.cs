using Hermes.Application.DTOs.Email;
using Hermes.Notifications.Sending.HtmlLayout.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Hermes.UnitTests.Notifications;

/// <summary>
/// Contains unit tests for <see cref="NewsletterHtmlService"/> and <see cref="NewsletterHtmlHelper"/>,
/// verifying time-of-day greetings, user display name formatting, HTML escaping, and embedded template rendering.
/// </summary>
public sealed class NewsletterHtmlServiceTests
{
    /// <summary>
    /// Tests that <see cref="NewsletterHtmlService.RenderNewsletterAsync"/> throws <see cref="ArgumentNullException"/>
    /// when the request is null.
    /// </summary>
    [Fact]
    public async Task RenderNewsletterAsync_Should_ThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        NewsletterHtmlService sut = new(TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RenderNewsletterAsync(null!));
    }

    /// <summary>
    /// Tests time-of-day greeting branches based on the hour of the day (Morning < 12, Afternoon < 18, Evening >= 18).
    /// </summary>
    [Theory]
    [InlineData(8, "Guten Morgen")]
    [InlineData(14, "Guten Tag")]
    [InlineData(20, "Guten Abend")]
    public async Task RenderNewsletterAsync_Should_SelectCorrectGreeting_BasedOnTimeOfDay(int hour, string expectedGreeting)
    {
        // Arrange
        DateTime fixedInstant = new(2026, 8, 17, hour, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = new(fixedInstant);
        NewsletterHtmlService sut = new(timeProvider);

        NewsletterRenderRequestDto request = new(
            UserDisplayName: "Erika",
            Articles: [
                new NewsletterArticleItemDto("Tech", "Title 1", "Content 1", "https://news/1", "https://img/1")
            ]);

        // Act
        string html = await sut.RenderNewsletterAsync(request);

        // Assert
        Assert.Contains($"{expectedGreeting}, Erika!", html);
    }

    /// <summary>
    /// Tests that when <see cref="NewsletterRenderRequestDto.UserDisplayName"/> is null or whitespace,
    /// the greeting omits the name segment.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderNewsletterAsync_Should_OmitNameInGreeting_WhenDisplayNameBlank(string? blankName)
    {
        // Arrange
        DateTime fixedInstant = new(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = new(fixedInstant);
        NewsletterHtmlService sut = new(timeProvider);

        NewsletterRenderRequestDto request = new(
            UserDisplayName: blankName,
            Articles: [
                new NewsletterArticleItemDto("Tech", "Title 1", "Content 1", "https://news/1", "https://img/1")
            ]);

        // Act
        string html = await sut.RenderNewsletterAsync(request);

        // Assert
        Assert.Contains("Guten Morgen! Hier sind die wichtigsten Nachrichten.", html);
    }

    /// <summary>
    /// Tests that HTML special characters (e.g. &lt;, &gt;, &amp;, &quot;) in article fields and headers are properly HTML-encoded.
    /// </summary>
    [Fact]
    public async Task RenderNewsletterAsync_Should_HtmlEncodeArticleFields()
    {
        // Arrange
        NewsletterHtmlService sut = new(TimeProvider.System);
        NewsletterRenderRequestDto request = new(
            UserDisplayName: "<Admin & Tester>",
            Articles: [
                new NewsletterArticleItemDto("Tech & Science", "<b>Bold Title</b>", "Description with \"quotes\" & <tags>", "https://news?a=1&b=2", "https://img?x=1&y=2")
            ]);

        // Act
        string html = await sut.RenderNewsletterAsync(request);

        // Assert
        Assert.Contains("&lt;Admin &amp; Tester&gt;", html);
        Assert.Contains("Tech &amp; Science", html);
        Assert.Contains("&lt;b&gt;Bold Title&lt;/b&gt;", html);
        Assert.Contains("Description with &quot;quotes&quot; &amp; &lt;tags&gt;", html);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterHtmlService.RenderNewsletterAsync"/> respects the cancellation token.
    /// </summary>
    [Fact]
    public async Task RenderNewsletterAsync_Should_RespectCancellationToken()
    {
        // Arrange
        NewsletterHtmlService sut = new(TimeProvider.System);
        NewsletterRenderRequestDto request = new("User", [new("Cat", "Tit", "Con", "Url", "Img")]);
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.RenderNewsletterAsync(request, cts.Token));
    }
}
