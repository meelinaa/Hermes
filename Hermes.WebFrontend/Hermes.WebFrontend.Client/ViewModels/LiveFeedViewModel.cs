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
/// ViewModel managing live news feed exploration, multi-criteria filtering, and saving filter sets as email subscriptions.
/// </summary>
public sealed class LiveFeedViewModel(
    INewsFeedApiClient newsFeedApi,
    HttpClient http,
    AuthTokenStore authTokens,
    IToastNotificationService toastService)
{
    private static readonly Weekdays[] AllWeekdays = Enum.GetValues<Weekdays>().ToArray();

    /// <summary>Event raised whenever state changes to trigger UI re-renders.</summary>
    public event Action? StateChanged;

    /// <summary>Gets or sets the keyword search terms query string.</summary>
    public string Keywords { get; set; } = string.Empty;

    /// <summary>Gets the set of currently selected news categories.</summary>
    public HashSet<NewsCategory> SelectedCategories { get; } = [];

    /// <summary>Gets the set of currently selected languages.</summary>
    public HashSet<Language> SelectedLanguages { get; } = [];

    /// <summary>Gets the set of currently selected countries.</summary>
    public HashSet<Country> SelectedCountries { get; } = [];

    /// <summary>Gets summary text of selected categories for dropdown headers.</summary>
    public string CategorySummaryText => SelectionSummary(SelectedCategories, NewsEnumGermanDisplayConstants.CategoryDe, "Alle Kategorien");

    /// <summary>Gets summary text of selected languages for dropdown headers.</summary>
    public string LanguageSummaryText => SelectionSummary(SelectedLanguages, NewsEnumGermanDisplayConstants.LanguageDe, "Alle Sprachen");

    /// <summary>Gets summary text of selected countries for dropdown headers.</summary>
    public string CountrySummaryText => SelectionSummary(SelectedCountries, NewsEnumGermanDisplayConstants.CountryDe, "Alle Länder");

    /// <summary>Gets the list of articles retrieved for the current filter criteria.</summary>
    public IReadOnlyList<NewsArticleDto> Articles { get; private set; } = [];

    /// <summary>Gets whether articles are currently being fetched.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>Gets the user-facing error message if article fetching failed.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets whether the save subscription modal dialog is currently visible.</summary>
    public bool ShowSaveModal { get; private set; }

    /// <summary>Gets whether a save subscription request is in progress.</summary>
    public bool IsSaving { get; private set; }

    /// <summary>Gets any validation error encountered during subscription save.</summary>
    public string? SaveError { get; private set; }

    /// <summary>Gets the active delivery weekday selection map for the subscription dialog.</summary>
    public Dictionary<Weekdays, bool> SubscriptionDays { get; } = AllWeekdays.ToDictionary(
        d => d,
        d => d is Weekdays.Monday or Weekdays.Tuesday or Weekdays.Wednesday or Weekdays.Thursday or Weekdays.Friday);

    /// <summary>Gets the delivery time slots list for the subscription dialog.</summary>
    public List<string> SubscriptionTimes { get; } = ["08:00"];

    /// <summary>Gets or sets whether email dispatch is active for the created subscription.</summary>
    public bool SubscriptionEnabled { get; set; } = true;

    /// <summary>
    /// Initializes default preview filters and loads initial news articles.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (SelectedCategories.Count == 0 && string.IsNullOrWhiteSpace(Keywords))
        {
            SelectedCategories.Add(NewsCategory.Technology);
        }

        await LoadArticlesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Queries the API for latest articles matching active search and filter criteria.
    /// </summary>
    public async Task LoadArticlesAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            NewsPreviewRequestDto request = new()
            {
                Keywords = string.IsNullOrWhiteSpace(Keywords) ? null : Keywords.Trim(),
                Categories = SelectedCategories.Count > 0 ? SelectedCategories.ToList() : null,
                Languages = SelectedLanguages.Count > 0 ? SelectedLanguages.ToList() : null,
                Countries = SelectedCountries.Count > 0 ? SelectedCountries.ToList() : null
            };

            ApiResult<IReadOnlyList<NewsArticleDto>> result = await newsFeedApi.GetPreviewArticlesAsync(request).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                Articles = result.Value ?? [];
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Fehler beim Laden der News-Artikel.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fehler beim Laden der News: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Toggles selection of a news category filter.
    /// </summary>
    public void ToggleCategory(NewsCategory category, bool isChecked)
    {
        if (isChecked)
            SelectedCategories.Add(category);
        else
            SelectedCategories.Remove(category);

        NotifyStateChanged();
    }

    /// <summary>
    /// Toggles selection of a news language filter.
    /// </summary>
    public void ToggleLanguage(Language language, bool isChecked)
    {
        if (isChecked)
            SelectedLanguages.Add(language);
        else
            SelectedLanguages.Remove(language);

        NotifyStateChanged();
    }

    /// <summary>
    /// Toggles selection of a news country filter.
    /// </summary>
    public void ToggleCountry(Country country, bool isChecked)
    {
        if (isChecked)
            SelectedCountries.Add(country);
        else
            SelectedCountries.Remove(country);

        NotifyStateChanged();
    }

    /// <summary>
    /// Clears all active search keywords and multi-select filter criteria.
    /// </summary>
    public void ClearFilters()
    {
        Keywords = string.Empty;
        SelectedCategories.Clear();
        SelectedLanguages.Clear();
        SelectedCountries.Clear();
        NotifyStateChanged();
    }

    /// <summary>
    /// Opens the save subscription modal pre-filled with current filter parameters.
    /// </summary>
    public void OpenSaveModal()
    {
        SaveError = null;
        ShowSaveModal = true;
        NotifyStateChanged();
    }

    /// <summary>
    /// Closes the save subscription modal.
    /// </summary>
    public void CloseSaveModal()
    {
        ShowSaveModal = false;
        SaveError = null;
        NotifyStateChanged();
    }

    /// <summary>
    /// Toggles the inclusion of a weekday in the subscription dispatch schedule.
    /// </summary>
    public void ToggleSubscriptionDay(Weekdays day)
    {
        if (SubscriptionDays.TryGetValue(day, out bool current))
        {
            SubscriptionDays[day] = !current;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Adds a new delivery time slot to the subscription schedule.
    /// </summary>
    public void AddSubscriptionTimeSlot()
    {
        SubscriptionTimes.Add("12:00");
        NotifyStateChanged();
    }

    /// <summary>
    /// Removes a delivery time slot at the specified list index.
    /// </summary>
    public void RemoveSubscriptionTimeSlot(int index)
    {
        if (index >= 0 && index < SubscriptionTimes.Count)
        {
            SubscriptionTimes.RemoveAt(index);
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Updates the time string (HH:mm) at the specified time slot index.
    /// </summary>
    public void SetSubscriptionTime(int index, string value)
    {
        if (index >= 0 && index < SubscriptionTimes.Count)
        {
            SubscriptionTimes[index] = value;
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Persists the active live feed filter configuration as a permanent email subscription.
    /// </summary>
    public async Task SaveAsSubscriptionAsync()
    {
        SaveError = null;
        await authTokens.EnsureLoadedFromStorageAsync().ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
        {
            SaveError = "Sitzung abgelaufen. Bitte erneut anmelden.";
            NotifyStateChanged();
            return;
        }

        List<Weekdays> weekdays = SubscriptionDays.Where(p => p.Value).Select(p => p.Key).OrderBy(d => (int)d).ToList();
        if (weekdays.Count == 0)
        {
            SaveError = "Bitte mindestens einen Wochentag auswählen.";
            NotifyStateChanged();
            return;
        }

        List<TimeOnly> times = [];
        foreach (string raw in SubscriptionTimes)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            string s = raw.Trim();
            string normalized = s.Length >= 5 ? s[..5] : s;
            if (TimeOnly.TryParseExact(normalized, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out TimeOnly parsed))
            {
                if (!times.Contains(parsed))
                    times.Add(parsed);
            }
            else
            {
                SaveError = $"Ungültiges Zeitformat '{raw}'. Bitte HH:mm verwenden.";
                NotifyStateChanged();
                return;
            }
        }

        if (times.Count == 0)
        {
            SaveError = "Bitte mindestens eine gültige Uhrzeit angeben.";
            NotifyStateChanged();
            return;
        }

        IsSaving = true;
        NotifyStateChanged();

        try
        {
            List<string>? keywords = !string.IsNullOrWhiteSpace(Keywords)
                ? Keywords.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : null;

            CreateNewsletterSubscriptionPayloadDto payload = new()
            {
                Keywords = keywords is { Count: > 0 } ? keywords : null,
                Category = SelectedCategories.Count > 0 ? SelectedCategories.ToList() : null,
                Languages = SelectedLanguages.Count > 0 ? SelectedLanguages.ToList() : null,
                Countries = SelectedCountries.Count > 0 ? SelectedCountries.ToList() : null,
                SendOnWeekdays = weekdays,
                SendAtTimes = times,
                IsEnabled = SubscriptionEnabled
            };

            HttpResponseMessage response = await http.PostAsJsonAsync(
                $"api/v1/users/{userId.Value}/newsletter-subscriptions",
                payload,
                HermesNewsJsonMapper.Options).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                ShowSaveModal = false;
                toastService.ShowSuccess("News-Abonnement erfolgreich erstellt! Du erhältst diese Updates nun per E-Mail.", "Abonnement aktiv");
            }
            else
            {
                var (errorMessage, _, _) = await ApiResponseReader.ReadErrorAsync(response).ConfigureAwait(false);
                SaveError = errorMessage;
                toastService.ShowError(errorMessage, "Speichern fehlgeschlagen");
            }
        }
        catch (Exception ex)
        {
            SaveError = $"Fehler beim Speichern: {ex.Message}";
            toastService.ShowError(SaveError, "Fehler");
        }
        finally
        {
            IsSaving = false;
            NotifyStateChanged();
        }
    }

    private static string SelectionSummary<T>(HashSet<T> selected, Func<T, string> labelDe, string emptyText)
    {
        if (selected.Count == 0)
            return emptyText;
        return string.Join(", ", selected.OrderBy(labelDe).Select(labelDe));
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
