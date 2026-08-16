using Bunit;
using Hermes.WebFrontend.Client.Components.News;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.News;

/// <summary>
/// bUnit tests verifying time input rendering, editing, addition, and removal callbacks in <see cref="TimeSlotPicker"/>.
/// </summary>
public sealed class TimeSlotPickerTests : BunitContext
{
    [Fact]
    public void Renders_Initial_Time_Slots()
    {
        // Arrange & Act
        var cut = Render<TimeSlotPicker>(parameters => parameters
            .Add(p => p.SharedTimes, ["08:00", "14:30"])
            .Add(p => p.OnSetTime, (_, _) => { })
            .Add(p => p.OnAddTimeSlot, () => { })
            .Add(p => p.OnRemoveTimeSlot, _ => { }));

        // Assert
        var inputs = cut.FindAll("input[type='time']");
        Assert.Equal(2, inputs.Count);
        Assert.Equal("08:00", inputs[0].GetAttribute("value"));
        Assert.Equal("14:30", inputs[1].GetAttribute("value"));
    }

    [Fact]
    public void Changing_Time_Input_Invokes_OnSetTime()
    {
        // Arrange
        int? editedIndex = null;
        string? editedValue = null;

        var cut = Render<TimeSlotPicker>(parameters => parameters
            .Add(p => p.SharedTimes, ["09:00"])
            .Add(p => p.OnSetTime, (index, val) =>
            {
                editedIndex = index;
                editedValue = val;
            })
            .Add(p => p.OnAddTimeSlot, () => { })
            .Add(p => p.OnRemoveTimeSlot, _ => { }));

        // Act
        var input = cut.Find("input[type='time']");
        input.Change("11:45");

        // Assert
        Assert.Equal(0, editedIndex);
        Assert.Equal("11:45", editedValue);
    }

    [Fact]
    public void Clicking_Add_Button_Invokes_OnAddTimeSlot()
    {
        // Arrange
        bool addInvoked = false;

        var cut = Render<TimeSlotPicker>(parameters => parameters
            .Add(p => p.SharedTimes, ["09:00"])
            .Add(p => p.OnSetTime, (_, _) => { })
            .Add(p => p.OnAddTimeSlot, () => addInvoked = true)
            .Add(p => p.OnRemoveTimeSlot, _ => { }));

        // Act
        var addBtn = cut.Find("button.btn-outline-primary");
        addBtn.Click();

        // Assert
        Assert.True(addInvoked);
    }

    [Fact]
    public void Clicking_Remove_Button_Invokes_OnRemoveTimeSlot()
    {
        // Arrange
        int? removedIndex = null;

        var cut = Render<TimeSlotPicker>(parameters => parameters
            .Add(p => p.SharedTimes, ["08:00", "12:00"])
            .Add(p => p.OnSetTime, (_, _) => { })
            .Add(p => p.OnAddTimeSlot, () => { })
            .Add(p => p.OnRemoveTimeSlot, idx => removedIndex = idx));

        // Act
        var removeButtons = cut.FindAll("button.btn-outline-secondary");
        Assert.Equal(2, removeButtons.Count);
        removeButtons[1].Click();

        // Assert
        Assert.Equal(1, removedIndex);
    }
}
