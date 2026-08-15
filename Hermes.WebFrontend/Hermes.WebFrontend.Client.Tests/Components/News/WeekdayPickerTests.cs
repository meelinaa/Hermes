using Bunit;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Components.News;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.News;

/// <summary>
/// bUnit tests verifying weekday pill rendering, active state CSS classes, and toggle callbacks in <see cref="WeekdayPicker"/>.
/// </summary>
public sealed class WeekdayPickerTests : BunitContext
{
    [Fact]
    public void Renders_All_Seven_Weekdays_With_Labels()
    {
        // Arrange
        Dictionary<Weekdays, bool> dayActive = new()
        {
            [Weekdays.Monday] = true,
            [Weekdays.Tuesday] = false,
            [Weekdays.Wednesday] = false,
            [Weekdays.Thursday] = false,
            [Weekdays.Friday] = true,
            [Weekdays.Saturday] = false,
            [Weekdays.Sunday] = false
        };

        // Act
        var cut = Render<WeekdayPicker>(parameters => parameters
            .Add(p => p.DayActive, dayActive)
            .Add(p => p.OnToggleDay, _ => { }));

        // Assert
        var buttons = cut.FindAll("button.news-dow-pill");
        Assert.Equal(7, buttons.Count);
        Assert.Contains("Mo", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Fr", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("So", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Applies_Active_Class_Only_To_Active_Days()
    {
        // Arrange
        Dictionary<Weekdays, bool> dayActive = new()
        {
            [Weekdays.Monday] = true,
            [Weekdays.Tuesday] = false,
            [Weekdays.Wednesday] = false,
            [Weekdays.Thursday] = false,
            [Weekdays.Friday] = false,
            [Weekdays.Saturday] = false,
            [Weekdays.Sunday] = false
        };

        // Act
        var cut = Render<WeekdayPicker>(parameters => parameters
            .Add(p => p.DayActive, dayActive)
            .Add(p => p.OnToggleDay, _ => { }));

        // Assert
        var activeButtons = cut.FindAll("button.news-dow-pill.active");
        Assert.Single(activeButtons);
        Assert.Contains("Mo", activeButtons[0].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Clicking_Pill_Invokes_OnToggleDay()
    {
        // Arrange
        Weekdays? toggledDay = null;
        Dictionary<Weekdays, bool> dayActive = new();

        var cut = Render<WeekdayPicker>(parameters => parameters
            .Add(p => p.DayActive, dayActive)
            .Add(p => p.OnToggleDay, d => toggledDay = d));

        // Act - Click on Friday (5th button)
        var buttons = cut.FindAll("button.news-dow-pill");
        buttons[4].Click();

        // Assert
        Assert.Equal(Weekdays.Friday, toggledDay);
    }
}
