using Bunit;
using Hermes.WebFrontend.Client.Components.News;
using Hermes.WebFrontend.Client.Components.UI;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.UI;

/// <summary>
/// bUnit tests verifying placeholder styling and structured card rendering in <see cref="Skeleton"/> and <see cref="NewsCardSkeleton"/>.
/// </summary>
public sealed class SkeletonTests : BunitContext
{
    [Fact]
    public void Skeleton_Renders_Custom_Dimensions_And_Styles()
    {
        // Act
        var cut = Render<Skeleton>(parameters => parameters
            .Add(p => p.Width, "200px")
            .Add(p => p.Height, "24px")
            .Add(p => p.BorderRadius, "8px")
            .Add(p => p.Class, "test-custom-class"));

        // Assert
        var el = cut.Find("div.hermes-skeleton");
        Assert.Contains("test-custom-class", el.ClassName, StringComparison.Ordinal);
        var style = el.GetAttribute("style");
        Assert.Contains("width: 200px", style, StringComparison.Ordinal);
        Assert.Contains("height: 24px", style, StringComparison.Ordinal);
        Assert.Contains("border-radius: 8px", style, StringComparison.Ordinal);
    }

    [Fact]
    public void NewsCardSkeleton_Renders_Specified_Count_Of_Cards()
    {
        // Act
        var cut = Render<NewsCardSkeleton>(parameters => parameters
            .Add(p => p.Count, 4));

        // Assert
        var cards = cut.FindAll("li.news-settings-card--skeleton");
        Assert.Equal(4, cards.Count);
    }
}
