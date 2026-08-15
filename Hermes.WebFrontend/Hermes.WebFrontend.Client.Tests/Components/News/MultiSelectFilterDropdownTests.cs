using Bunit;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Components.News;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.News;

/// <summary>
/// bUnit tests verifying rendering, search filtering, and toggle callbacks in <see cref="MultiSelectFilterDropdown{TItem}"/>.
/// </summary>
public sealed class MultiSelectFilterDropdownTests : BunitContext
{
    public MultiSelectFilterDropdownTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_Label_And_SummaryText()
    {
        // Arrange & Act
        var cut = Render<MultiSelectFilterDropdown<NewsCategory>>(parameters => parameters
            .Add(p => p.Label, "Test Kategorien")
            .Add(p => p.SummaryText, "2 ausgewählt")
            .Add(p => p.AllItems, [NewsCategory.Technology, NewsCategory.Sports])
            .Add(p => p.SelectedItems, [NewsCategory.Technology])
            .Add(p => p.GetDisplayName, c => c.ToString())
            .Add(p => p.OnToggle, (_, _) => { }));

        // Assert
        Assert.Contains("Test Kategorien", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("2 ausgewählt", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Filters_Options_On_Search_Input()
    {
        // Arrange
        var cut = Render<MultiSelectFilterDropdown<NewsCategory>>(parameters => parameters
            .Add(p => p.Label, "Kategorien")
            .Add(p => p.SummaryText, "Alle")
            .Add(p => p.AllItems, [NewsCategory.Technology, NewsCategory.Sports, NewsCategory.Business])
            .Add(p => p.SelectedItems, [])
            .Add(p => p.GetDisplayName, c => c switch
            {
                NewsCategory.Technology => "Technologie",
                NewsCategory.Sports => "Sport",
                NewsCategory.Business => "Wirtschaft",
                _ => c.ToString()
            })
            .Add(p => p.OnToggle, (_, _) => { }));

        // Act - search for "tech"
        var searchInput = cut.Find("input[type='search']");
        searchInput.Input("tech");

        // Assert
        Assert.Contains("Technologie", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Sport", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Wirtschaft", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Toggling_Checkbox_Invokes_OnToggle_Callback()
    {
        // Arrange
        NewsCategory? toggledCategory = null;
        bool? toggledState = null;

        var cut = Render<MultiSelectFilterDropdown<NewsCategory>>(parameters => parameters
            .Add(p => p.Label, "Kategorien")
            .Add(p => p.SummaryText, "Keine")
            .Add(p => p.AllItems, [NewsCategory.Technology])
            .Add(p => p.SelectedItems, [])
            .Add(p => p.GetDisplayName, c => c.ToString())
            .Add(p => p.OnToggle, (cat, isChecked) =>
            {
                toggledCategory = cat;
                toggledState = isChecked;
            }));

        // Act
        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.Change(true);

        // Assert
        Assert.Equal(NewsCategory.Technology, toggledCategory);
        Assert.True(toggledState);
    }
}
