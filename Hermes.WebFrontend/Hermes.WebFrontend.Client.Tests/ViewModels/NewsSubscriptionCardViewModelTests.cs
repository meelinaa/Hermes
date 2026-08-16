using System.Net;
using System.Text;
using System.Text.Json;
using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Enums;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

using Hermes.WebFrontend.Client.Services.Notifications;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying subscription form validation, multi-select toggles, and save workflows in <see cref="NewsSubscriptionCardViewModel"/>.
/// </summary>
public sealed class NewsSubscriptionCardViewModelTests
{
    private const string ValidTestJwtToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI0MiIsIm5hbWUiOiJ0ZXN0ZXIifQ.dummy";

    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly Mock<IToastNotificationService> _toastMock = new();

    private NewsSubscriptionCardViewModel CreateSut(HttpMessageHandler? handler = null)
    {
        _localStorageMock.Setup(s => s.GetItemAsync<string>("hermes.auth.accessToken", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidTestJwtToken);
        AuthTokenStore tokenStore = new(_localStorageMock.Object);
        HttpClient client = handler != null ? new HttpClient(handler) : new HttpClient();
        client.BaseAddress = new Uri("http://localhost/");
        return new NewsSubscriptionCardViewModel(client, tokenStore, _toastMock.Object);
    }

    [Fact]
    public void ApplyInitialModel_Null_Should_ResetToDefaults()
    {
        // Arrange
        NewsSubscriptionCardViewModel sut = CreateSut();
        sut.KeywordsRaw = "Something";
        sut.ToggleCategory(NewsCategory.Business, true);

        // Act
        sut.ApplyInitialModel(null);

        // Assert
        Assert.Equal(0, sut.EditingId);
        Assert.False(sut.IsEditing);
        Assert.Equal(string.Empty, sut.KeywordsRaw);
        Assert.Empty(sut.SelectedCategories);
        Assert.Single(sut.SharedTimes);
        Assert.Equal("10:00", sut.SharedTimes[0]);
    }

    [Fact]
    public void ApplyInitialModel_ExistingDto_Should_PopulateFields()
    {
        // Arrange
        NewsSubscriptionCardViewModel sut = CreateSut();
        NewsSubscriptionDto dto = new()
        {
            Id = 42,
            Keywords = ["Tech", "Climate"],
            Category = [NewsCategory.Technology],
            Languages = [Language.German],
            Countries = [Country.Germany],
            SendOnWeekdays = [Weekdays.Monday, Weekdays.Friday],
            SendAtTimes = [new TimeOnly(9, 15)],
            IsEnabled = false
        };

        // Act
        sut.ApplyInitialModel(dto);

        // Assert
        Assert.Equal(42, sut.EditingId);
        Assert.True(sut.IsEditing);
        Assert.Equal("Tech, Climate", sut.KeywordsRaw);
        Assert.Contains(NewsCategory.Technology, sut.SelectedCategories);
        Assert.Contains(Language.German, sut.SelectedLanguages);
        Assert.Contains(Country.Germany, sut.SelectedCountries);
        Assert.True(sut.DayActive[Weekdays.Monday]);
        Assert.True(sut.DayActive[Weekdays.Friday]);
        Assert.False(sut.DayActive[Weekdays.Tuesday]);
        Assert.Single(sut.SharedTimes);
        Assert.Equal("09:15", sut.SharedTimes[0]);
        Assert.False(sut.IsEnabled);
    }

    [Fact]
    public void MultiSelectToggles_Should_AddAndRemoveSelections()
    {
        // Arrange
        NewsSubscriptionCardViewModel sut = CreateSut();

        // Act & Assert Category
        sut.ToggleCategory(NewsCategory.Sports, true);
        Assert.Contains(NewsCategory.Sports, sut.SelectedCategories);
        sut.ToggleCategory(NewsCategory.Sports, false);
        Assert.DoesNotContain(NewsCategory.Sports, sut.SelectedCategories);

        // Act & Assert Language
        sut.ToggleLanguage(Language.English, true);
        Assert.Contains(Language.English, sut.SelectedLanguages);
        sut.ToggleLanguage(Language.English, false);
        Assert.DoesNotContain(Language.English, sut.SelectedLanguages);

        // Act & Assert Country
        sut.ToggleCountry(Country.Austria, true);
        Assert.Contains(Country.Austria, sut.SelectedCountries);
        sut.ToggleCountry(Country.Austria, false);
        Assert.DoesNotContain(Country.Austria, sut.SelectedCountries);
    }

    [Fact]
    public void TimeSlotManagement_Should_AddRemoveAndNormalize()
    {
        // Arrange
        NewsSubscriptionCardViewModel sut = CreateSut();
        Assert.Single(sut.SharedTimes);

        // Act 1: Add
        sut.AddSharedTimeSlot();
        Assert.Equal(2, sut.SharedTimes.Count);

        // Act 2: Edit
        sut.SetSharedTime(1, "14:30:00");
        Assert.Equal("14:30", sut.SharedTimes[1]);

        // Act 3: Remove
        sut.RemoveSharedTimeSlot(1);
        Assert.Single(sut.SharedTimes);
    }

    [Fact]
    public async Task SaveNewsConfigurationAsync_Should_FailValidation_When_NoWeekdaysSelected()
    {
        // Arrange
        NewsSubscriptionCardViewModel sut = CreateSut();
        // by default all weekdays are false

        // Act
        var (success, _) = await sut.SaveNewsConfigurationAsync();

        // Assert
        Assert.False(success);
        Assert.Equal("Bitte mindestens einen Sendetag wählen.", sut.NewsFeedback);
    }

    [Fact]
    public async Task SaveNewsConfigurationAsync_Should_Succeed_When_ValidNewSubscription()
    {
        // Arrange
        Mock<HttpMessageHandler> handlerMock = new();
        CreateNewsletterSubscriptionResponseDto responseDto = new() { SubscriptionId = 99, UserId = 42 };
        HttpResponseMessage httpResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(responseDto), Encoding.UTF8, "application/json")
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);

        NewsSubscriptionCardViewModel sut = CreateSut(handlerMock.Object);
        sut.KeywordsRaw = "dotnet, csharp";
        sut.ToggleDay(Weekdays.Monday);
        sut.SetSharedTime(0, "08:00");

        // Act
        var (success, createdId) = await sut.SaveNewsConfigurationAsync();

        // Assert
        Assert.True(success);
        Assert.Equal(99, createdId);
        Assert.Contains("Newsletter-Abonnement #99", sut.NewsFeedback);
    }
}
