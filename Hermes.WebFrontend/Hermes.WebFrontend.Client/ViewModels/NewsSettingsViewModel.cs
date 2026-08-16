using System.Globalization;
using System.Net.Http.Json;
using Hermes.WebFrontend.Client.ApiModels;
using Hermes.WebFrontend.Client.ApiModels.Enums;
using Hermes.WebFrontend.Client.Enums;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.Services.Notifications;
using Hermes.WebFrontend.Client.Services.User;

namespace Hermes.WebFrontend.Client.ViewModels;

/// <summary>
/// ViewModel managing newsletter subscription list filtering, pagination, toggling, deletion, and editor navigation.
/// </summary>
public sealed class NewsSettingsViewModel(
    NewsSubscriptionApiClient newsListCache,
    HttpClient http,
    AuthTokenStore authTokens,
    IToastNotificationService toastService)
{
    /// <summary>Event raised whenever ViewModel state changes to trigger UI updates.</summary>
    public event Action? StateChanged;

    /// <summary>Gets the list of active subscription items for the current page.</summary>
    public List<NewsSubscriptionDto> Items { get; private set; } = [];

    /// <summary>Gets or sets whether the create/edit editor form is currently visible.</summary>
    public bool ShowForm { get; set; }

    /// <summary>Gets or sets the subscription model currently being edited.</summary>
    public NewsSubscriptionDto? EditModel { get; set; }

    /// <summary>Gets or sets whether subscriptions are currently being fetched.</summary>
    public bool Loading { get; set; } = true;

    /// <summary>Gets or sets error message when fetching fails.</summary>
    public string? LoadError { get; set; }

    /// <summary>Gets or sets the ID of the item currently being deleted.</summary>
    public int? DeletingId { get; set; }

    /// <summary>Gets or sets the ID of the item whose active status is currently being toggled.</summary>
    public int? TogglingId { get; set; }

    /// <summary>Gets or sets the current page number (1-based).</summary>
    public int CurrentPage { get; set; } = 1;

    /// <summary>Gets or sets the number of items per page.</summary>
    public int PageSize { get; set; } = 20;

    /// <summary>Gets or sets whether the filter bar is visible.</summary>
    public bool ShowFilters { get; set; }

    /// <summary>Gets or sets whether items are sorted descending by ID.</summary>
    public bool SortDescending { get; set; }

    /// <summary>Gets or sets the keyword search filter input.</summary>
    public string SearchInput { get; set; } = string.Empty;

    /// <summary>Gets or sets the category filter value.</summary>
    public NewsCategory? CategoryFilter { get; set; }

    /// <summary>Gets the total count of matching subscriptions across all pages.</summary>
    public int? TotalCount { get; private set; }

    /// <summary>Gets the total number of pages.</summary>
    public int? TotalPages { get; private set; }

    /// <summary>Gets whether another page of results exists.</summary>
    public bool HasNextPage { get; private set; }

    /// <summary>Gets whether any search text or category filters are active.</summary>
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchInput) || CategoryFilter is not null;

    /// <summary>Gets the selected category value formatted for HTML select elements.</summary>
    public string CategorySelectValue => CategoryFilter is NewsCategory c ? ((int)c).ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Gets the CSS class list for the filter toggle button.</summary>
    public string FilterToggleClass =>
        "news-settings-filter-toggle"
        + (ShowFilters ? " news-settings-filter-toggle--open" : string.Empty)
        + (HasActiveFilters ? " news-settings-filter-toggle--active" : string.Empty);

    /// <summary>
    /// Initializes the list and optionally navigates directly to the creation form.
    /// </summary>
    public async Task InitializeAsync(bool startInCreateMode = false, CancellationToken cancellationToken = default)
    {
        await LoadListAsync(cancellationToken).ConfigureAwait(false);
        if (startInCreateMode)
            StartCreate();
    }

    /// <summary>Toggles the filter bar visibility.</summary>
    public void ToggleFilters()
    {
        ShowFilters = !ShowFilters;
        NotifyStateChanged();
    }

    /// <summary>Fetches the list of newsletter subscriptions from the API using current filters and pagination.</summary>
    public async Task LoadListAsync(CancellationToken cancellationToken = default)
    {
        Loading = true;
        LoadError = null;
        NotifyStateChanged();

        await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
        {
            LoadError = "Bitte erneut anmelden.";
            Items = [];
            TotalCount = null;
            TotalPages = null;
            HasNextPage = false;
            Loading = false;
            NotifyStateChanged();
            return;
        }

        string? q = string.IsNullOrWhiteSpace(SearchInput) ? null : SearchInput.Trim();
        (NewsListPageDto? data, string? err) = await newsListCache.FetchAsync(
            userId.Value,
            http,
            CurrentPage,
            PageSize,
            SortDescending,
            q,
            CategoryFilter,
            afterId: null).ConfigureAwait(false);

        if (err is not null)
        {
            Items = [];
            TotalCount = null;
            TotalPages = null;
            HasNextPage = false;
            LoadError = err;
        }
        else if (data is not null)
        {
            Items = data.Items ?? [];
            TotalCount = data.TotalCount;
            TotalPages = data.TotalPages;
            HasNextPage = data.HasNextPage;
            CurrentPage = data.Page;
        }

        Loading = false;
        NotifyStateChanged();
    }

    /// <summary>Updates the category filter and reloads from the first page.</summary>
    public Task SetCategoryFilterAsync(NewsCategory? category)
    {
        CategoryFilter = category;
        CurrentPage = 1;
        return LoadListAsync();
    }

    /// <summary>Updates the sort direction and reloads from the first page.</summary>
    public Task SetSortDescendingAsync(bool descending)
    {
        SortDescending = descending;
        CurrentPage = 1;
        return LoadListAsync();
    }

    /// <summary>Updates the page size and reloads from the first page.</summary>
    public Task SetPageSizeAsync(int size)
    {
        if (size is 10 or 20 or 50)
        {
            PageSize = size;
            CurrentPage = 1;
        }
        return LoadListAsync();
    }

    /// <summary>Applies text search and category filters and reloads from the first page.</summary>
    public Task ApplyFiltersAsync()
    {
        CurrentPage = 1;
        return LoadListAsync();
    }

    /// <summary>Navigates to the previous page of subscriptions.</summary>
    public async Task PrevPageAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentPage <= 1)
            return;
        CurrentPage--;
        await LoadListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Navigates to the next page of subscriptions.</summary>
    public async Task NextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!HasNextPage)
            return;
        CurrentPage++;
        await LoadListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the subscription editor in create mode.</summary>
    public void StartCreate()
    {
        EditModel = null;
        ShowForm = true;
        NotifyStateChanged();
    }

    /// <summary>Opens the subscription editor for the selected subscription.</summary>
    public void StartEdit(NewsSubscriptionDto n)
    {
        EditModel = n;
        ShowForm = true;
        NotifyStateChanged();
    }

    /// <summary>Handles returning from the editor back to the subscription list.</summary>
    public async Task HandleReturnFromFormAsync(bool listChanged, CancellationToken cancellationToken = default)
    {
        ShowForm = false;
        EditModel = null;
        if (listChanged)
        {
            newsListCache.Invalidate();
            CurrentPage = 1;
            await LoadListAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            NotifyStateChanged();
        }
    }

    /// <summary>Toggles the enabled status of a newsletter subscription.</summary>
    public async Task ToggleEnabledAsync(NewsSubscriptionDto n, bool targetEnabled, CancellationToken cancellationToken = default)
    {
        if (n.IsEnabled == targetEnabled)
            return;

        await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
        {
            LoadError = "Bitte erneut anmelden.";
            NotifyStateChanged();
            return;
        }

        TogglingId = n.Id;
        LoadError = null;
        NotifyStateChanged();

        try
        {
            UpdateNewsletterSubscriptionPayloadDto payload = new()
            {
                Id = n.Id,
                Keywords = n.Keywords,
                Category = n.Category,
                Languages = n.Languages,
                Countries = n.Countries,
                SendOnWeekdays = n.SendOnWeekdays,
                SendAtTimes = n.SendAtTimes,
                IsEnabled = targetEnabled,
            };

            using HttpResponseMessage response = await http.PutAsJsonAsync(
                $"api/v1/users/{userId.Value}/newsletter-subscriptions/{n.Id}",
                payload,
                HermesNewsJsonMapper.Options,
                cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                newsListCache.Invalidate();
                string statusText = targetEnabled ? "aktiviert" : "deaktiviert";
                toastService.ShowInfo($"Abonnement #{n.Id} {statusText}.", "News-Abonnements");
                await LoadListAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var (msg, _, _) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                LoadError = msg;
            }
        }
        finally
        {
            TogglingId = null;
            NotifyStateChanged();
        }
    }

    /// <summary>Deletes a newsletter subscription.</summary>
    public async Task DeleteAsync(NewsSubscriptionDto n, CancellationToken cancellationToken = default)
    {
        await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
            return;

        DeletingId = n.Id;
        NotifyStateChanged();

        try
        {
            using HttpResponseMessage response = await http.DeleteAsync(
                $"api/v1/users/{userId.Value}/newsletter-subscriptions/{n.Id}",
                cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                newsListCache.Invalidate();
                toastService.ShowSuccess($"Abonnement #{n.Id} erfolgreich gelöscht.", "News-Abonnements");
                await LoadListAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var (msg, _, _) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                LoadError = msg;
            }
        }
        finally
        {
            DeletingId = null;
            NotifyStateChanged();
        }
    }

    /// <summary>Generates display title for a subscription card.</summary>
    public static string TitleFor(NewsSubscriptionDto n) =>
        n.Keywords is { Count: > 0 } ? string.Join(", ", n.Keywords) : "Ohne Schlagworte";

    /// <summary>Formats keywords list for display.</summary>
    public static string FormatKeywords(NewsSubscriptionDto n) =>
        n.Keywords is { Count: > 0 } ? string.Join(", ", n.Keywords) : "—";

    /// <summary>Formats categories list for display in German.</summary>
    public static string FormatCategories(NewsSubscriptionDto n) =>
        n.Category is { Count: > 0 }
            ? string.Join(", ", n.Category.OrderBy(c => c.ToString()).Select(c => NewsEnumGermanDisplayConstants.CategoryDe(c)))
            : "—";

    /// <summary>Formats languages list for display in German.</summary>
    public static string FormatLanguages(NewsSubscriptionDto n) =>
        n.Languages is { Count: > 0 }
            ? string.Join(", ", n.Languages.OrderBy(l => l.ToString()).Select(l => NewsEnumGermanDisplayConstants.LanguageDe(l)))
            : "—";

    /// <summary>Formats countries list for display in German.</summary>
    public static string FormatCountries(NewsSubscriptionDto n) =>
        n.Countries is { Count: > 0 }
            ? string.Join(", ", n.Countries.OrderBy(c => c.ToString()).Select(c => NewsEnumGermanDisplayConstants.CountryDe(c)))
            : "—";

    /// <summary>Formats weekdays list for display.</summary>
    public static string FormatWeekdays(NewsSubscriptionDto n) =>
        n.SendOnWeekdays is { Count: > 0 } w
            ? string.Join(", ", w.OrderBy(d => (int)d).Select(ShortWeekday))
            : "—";

    /// <summary>Returns localized short weekday name.</summary>
    public static string ShortWeekday(Weekdays d) => d switch
    {
        Weekdays.Monday => "Mo",
        Weekdays.Tuesday => "Di",
        Weekdays.Wednesday => "Mi",
        Weekdays.Thursday => "Do",
        Weekdays.Friday => "Fr",
        Weekdays.Saturday => "Sa",
        Weekdays.Sunday => "So",
        _ => d.ToString()
    };

    /// <summary>Formats times list for display.</summary>
    public static string FormatTimes(NewsSubscriptionDto n) =>
        n.SendAtTimes is { Count: > 0 } t
            ? string.Join(", ", t.OrderBy(x => x).Select(x => x.ToString("HH:mm")))
            : "—";

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
