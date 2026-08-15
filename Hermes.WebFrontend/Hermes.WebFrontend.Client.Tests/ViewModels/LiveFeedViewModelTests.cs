using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.Notifications;
using Hermes.WebFrontend.Client.ViewModels;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.ViewModels;

/// <summary>
/// Unit tests verifying state management, filter mutations, and save workflows in <see cref="LiveFeedViewModel"/>.
/// </summary>
public sealed class LiveFeedViewModelTests
{
    private readonly Mock<INewsFeedApiClient> _newsFeedApiMock = new();
    private readonly Mock<IToastNotificationService> _toastMock = new();
    private readonly Mock<ILocalStorageService> _localStorageMock = new();
    private readonly AuthTokenStore _tokenStore;
    private readonly HttpClient _httpClient = new();
    private readonly LiveFeedViewModel _sut;

    public LiveFeedViewModelTests()
    {
        _tokenStore = new AuthTokenStore(_localStorageMock.Object);
        _sut = new LiveFeedViewModel(_newsFeedApiMock.Object, _httpClient, _tokenStore, _toastMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_Should_Not_Preselect_Categories_Or_Auto_Load_Articles()
    {
        // Act
        await _sut.InitializeAsync();

        // Assert
        Assert.Empty(_sut.SelectedCategories);
        Assert.Empty(_sut.Articles);
        Assert.False(_sut.HasSearched);
        Assert.False(_sut.IsLoading);
        Assert.Null(_sut.ErrorMessage);
    }

    [Fact]
    public async Task LoadArticlesAsync_Should_Query_Api_And_Populate_Articles_Matching_Filters()
    {
        // Arrange
        List<NewsArticleDto> articles =
        [
            new() { ArticleId = "1", Title = "Tech News", Category = ["technology"] }
        ];

        _newsFeedApiMock
            .Setup(api => api.GetPreviewArticlesAsync(It.IsAny<NewsPreviewRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResult<IReadOnlyList<NewsArticleDto>>.Success(articles));

        _sut.SelectedCategories.Add(NewsCategory.Technology);

        // Act
        await _sut.LoadArticlesAsync();

        // Assert
        Assert.True(_sut.HasSearched);
        Assert.Single(_sut.Articles);
        Assert.Equal("Tech News", _sut.Articles[0].Title);
        Assert.False(_sut.IsLoading);
        Assert.Null(_sut.ErrorMessage);
    }

    [Fact]
    public void ToggleCategory_Should_Add_And_Remove_Category()
    {
        // Act - Add
        _sut.ToggleCategory(NewsCategory.Science, true);
        Assert.Contains(NewsCategory.Science, _sut.SelectedCategories);

        // Act - Remove
        _sut.ToggleCategory(NewsCategory.Science, false);
        Assert.DoesNotContain(NewsCategory.Science, _sut.SelectedCategories);
    }

    [Fact]
    public void ToggleLanguage_And_ToggleCountry_Should_Update_Filter_Sets()
    {
        // Act - Languages
        _sut.ToggleLanguage(Language.German, true);
        Assert.Contains(Language.German, _sut.SelectedLanguages);
        _sut.ToggleLanguage(Language.German, false);
        Assert.DoesNotContain(Language.German, _sut.SelectedLanguages);

        // Act - Countries
        _sut.ToggleCountry(Country.Germany, true);
        Assert.Contains(Country.Germany, _sut.SelectedCountries);
        _sut.ToggleCountry(Country.Germany, false);
        Assert.DoesNotContain(Country.Germany, _sut.SelectedCountries);
    }

    [Fact]
    public void ClearFilters_Should_Reset_Keywords_And_All_Filter_Sets()
    {
        // Arrange
        _sut.Keywords = "Quantum";
        _sut.SelectedCategories.Add(NewsCategory.Technology);
        _sut.SelectedLanguages.Add(Language.English);
        _sut.SelectedCountries.Add(Country.USA);

        // Act
        _sut.ClearFilters();

        // Assert
        Assert.Empty(_sut.Keywords);
        Assert.Empty(_sut.SelectedCategories);
        Assert.Empty(_sut.SelectedLanguages);
        Assert.Empty(_sut.SelectedCountries);
    }

    [Fact]
    public void OpenSaveModal_And_CloseSaveModal_Should_Control_Modal_State()
    {
        // Act - Open
        _sut.OpenSaveModal();
        Assert.True(_sut.ShowSaveModal);

        // Act - Close
        _sut.CloseSaveModal();
        Assert.False(_sut.ShowSaveModal);
    }

    [Fact]
    public void Add_And_Remove_SubscriptionTimeSlot_Should_Update_Time_List()
    {
        // Arrange initial state has 1 time slot ("08:00")
        Assert.Single(_sut.SubscriptionTimes);

        // Act - Add
        _sut.AddSubscriptionTimeSlot();
        Assert.Equal(2, _sut.SubscriptionTimes.Count);

        // Act - Edit
        _sut.SetSubscriptionTime(1, "18:00");
        Assert.Equal("18:00", _sut.SubscriptionTimes[1]);

        // Act - Remove
        _sut.RemoveSubscriptionTimeSlot(1);
        Assert.Single(_sut.SubscriptionTimes);
    }
}
