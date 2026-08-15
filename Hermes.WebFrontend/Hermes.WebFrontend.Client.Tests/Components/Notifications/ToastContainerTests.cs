using Bunit;
using Hermes.WebFrontend.Client.Components.Notifications;
using Hermes.WebFrontend.Client.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.Notifications;

/// <summary>
/// bUnit tests verifying live toast stack updates and dismiss interactions in <see cref="ToastContainer"/>.
/// </summary>
public sealed class ToastContainerTests : BunitContext
{
    [Fact]
    public void Renders_Active_Toasts_And_Updates_On_New_Message()
    {
        // Arrange
        using ToastNotificationService toastService = new();
        Services.AddSingleton<IToastNotificationService>(toastService);

        var cut = Render<ToastContainer>();
        Assert.Empty(cut.FindAll("div.toast-item"));

        // Act - Post a toast
        toastService.ShowSuccess("Profil gespeichert!", "Erfolg");
        cut.Render();

        // Assert
        var toast = cut.Find("div.toast-item");
        Assert.Contains("toast-item--success", toast.ClassName, StringComparison.Ordinal);
        Assert.Contains("Erfolg", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Profil gespeichert!", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Clicking_Close_Button_Dismisses_Toast()
    {
        // Arrange
        using ToastNotificationService toastService = new();
        Services.AddSingleton<IToastNotificationService>(toastService);
        toastService.ShowError("Fehler beim Speichern.", "Fehler", durationMs: 0);

        var cut = Render<ToastContainer>();
        Assert.Single(cut.FindAll("div.toast-item"));

        // Act
        var closeBtn = cut.Find("button.toast-item__close");
        closeBtn.Click();
        cut.Render();

        // Assert
        Assert.Empty(cut.FindAll("div.toast-item"));
    }
}
