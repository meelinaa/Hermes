using System.Reflection;
using Hermes.Notifications.Sending.HtmlLayout.Builders;
using Hermes.Notifications.Sending.HtmlLayout.DTOs;
using Hermes.Notifications.Sending.HtmlLayout.Providers;
using Xunit;

namespace Hermes.UnitTests.Notifications;

/// <summary>
/// Contains unit tests for <see cref="EmbeddedTemplateProvider"/> and HTML helper argument validation.
/// </summary>
public sealed class EmbeddedTemplateProviderTests
{
    /// <summary>
    /// Tests that <see cref="EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync"/> successfully reads embedded HTML files.
    /// </summary>
    [Fact]
    public async Task ReadEmbeddedTemplateAsync_Should_ReadExistingResource()
    {
        // Arrange
        Assembly assembly = typeof(NewsletterHtmlHelper).Assembly;

        // Act
        string template = await EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "Verification.html", CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(template));
        Assert.Contains("{{VERIFICATION_CODE}}", template);
    }

    /// <summary>
    /// Tests that <see cref="EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync"/> throws <see cref="InvalidOperationException"/>
    /// when the specified resource name cannot be found in the assembly.
    /// </summary>
    [Fact]
    public async Task ReadEmbeddedTemplateAsync_Should_ThrowInvalidOperationException_WhenResourceNotFound()
    {
        // Arrange
        Assembly assembly = typeof(NewsletterHtmlHelper).Assembly;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "NonExistentTemplate.html", CancellationToken.None));

        Assert.Contains("NonExistentTemplate.html", ex.Message);
    }

    /// <summary>
    /// Tests that <see cref="NewsletterHtmlHelper.BuildAsync"/> throws <see cref="ArgumentNullException"/> when parameters are null.
    /// </summary>
    [Fact]
    public async Task NewsletterHtmlHelper_BuildAsync_Should_ThrowArgumentNullException_WhenArgumentsNull()
    {
        // Arrange
        NewsletterHeaderContentDto header = new("H", "H2", "D", "I");
        NewsletterFooterContentDto footer = new("F", "D", "S");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            NewsletterHtmlHelper.BuildAsync(null!, [], footer));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            NewsletterHtmlHelper.BuildAsync(header, null!, footer));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            NewsletterHtmlHelper.BuildAsync(header, [], null!));
    }

    /// <summary>
    /// Tests that <see cref="VerificationHtmlHelper.BuildAsync"/> throws <see cref="ArgumentNullException"/> when argument is null.
    /// </summary>
    [Fact]
    public async Task VerificationHtmlHelper_BuildAsync_Should_ThrowArgumentNullException_WhenArgumentNull()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            VerificationHtmlHelper.BuildAsync(null!));
    }
}
