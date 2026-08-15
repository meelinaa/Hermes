using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Enums;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Xunit;

using Microsoft.Extensions.Logging;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying subscription filtering, list navigation, and state operations in <see cref="NewsSettingsViewModel"/>.
/// </summary>
public sealed class NewsSettingsViewModelTests
{
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly NewsSubscriptionApiClient _newsClient = new(new Mock<ILogger<NewsSubscriptionApiClient>>().Object);

    private NewsSettingsViewModel CreateSut(HttpClient? httpClient = null)
    {
        AuthTokenStore store = new(_localStorageMock.Object);
        HttpClient client = httpClient ?? new HttpClient();
        return new NewsSettingsViewModel(_newsClient, client, store);
    }

    [Fact]
    public void FormattingHelpers_Should_FormatKeywordsCategoriesAndWeekdays()
    {
        // Arrange
        NewsSubscriptionDto dto = new()
        {
            Id = 1,
            Keywords = ["AI", "Tech"],
            Category = [NewsCategory.Technology, NewsCategory.Business],
            Languages = [Language.German],
            Countries = [Country.Germany],
            SendOnWeekdays = [Weekdays.Monday, Weekdays.Friday],
            SendAtTimes = [new TimeOnly(8, 30)],
            IsEnabled = true
        };

        // Assert
        Assert.Equal("AI, Tech", NewsSettingsViewModel.TitleFor(dto));
        Assert.Equal("AI, Tech", NewsSettingsViewModel.FormatKeywords(dto));
        Assert.Contains("Technologie", NewsSettingsViewModel.FormatCategories(dto));
        Assert.Contains("Deutsch", NewsSettingsViewModel.FormatLanguages(dto));
        Assert.Contains("Deutschland", NewsSettingsViewModel.FormatCountries(dto));
        Assert.Equal("Mo, Fr", NewsSettingsViewModel.FormatWeekdays(dto));
        Assert.Equal("08:30", NewsSettingsViewModel.FormatTimes(dto));
    }

    [Fact]
    public void ToggleFilters_Should_InvertShowFilters()
    {
        // Arrange
        NewsSettingsViewModel sut = CreateSut();
        Assert.False(sut.ShowFilters);

        // Act
        sut.ToggleFilters();

        // Assert
        Assert.True(sut.ShowFilters);
    }

    [Fact]
    public void StartCreate_And_StartEdit_Should_ConfigureEditorState()
    {
        // Arrange
        NewsSettingsViewModel sut = CreateSut();
        NewsSubscriptionDto dto = new() { Id = 42 };

        // Act 1: Create
        sut.StartCreate();
        Assert.True(sut.ShowForm);
        Assert.Null(sut.EditModel);

        // Act 2: Edit
        sut.StartEdit(dto);
        Assert.True(sut.ShowForm);
        Assert.Equal(42, sut.EditModel?.Id);
    }
}
