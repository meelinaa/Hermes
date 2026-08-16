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
/// ViewModel managing individual newsletter subscription creation, modification, validation, and multi-select filtering.
/// </summary>
public sealed class NewsSubscriptionCardViewModel(
    HttpClient http,
    AuthTokenStore authTokens,
    IToastNotificationService toastService)
{
    private static readonly Weekdays[] AllWeekdays = Enum.GetValues<Weekdays>().ToArray();

    /// <summary>Event raised whenever ViewModel state changes to trigger UI updates.</summary>
    public event Action? StateChanged;

    /// <summary>Gets the ID of the subscription being edited, or 0 if creating a new subscription.</summary>
    public int EditingId { get; private set; }

    /// <summary>Gets whether the form is in edit mode for an existing subscription.</summary>
    public bool IsEditing => EditingId > 0;

    /// <summary>Gets the localized header title text for the card.</summary>
    public string EditingLabel => IsEditing ? "News-Einstellungen bearbeiten" : "Neue News-Einstellung";

    /// <summary>Gets or sets raw comma-separated keywords input.</summary>
    public string KeywordsRaw { get; set; } = string.Empty;

    /// <summary>Gets the set of currently selected news categories.</summary>
    public HashSet<NewsCategory> SelectedCategories { get; } = [];

    /// <summary>Gets the set of currently selected news languages.</summary>
    public HashSet<Language> SelectedLanguages { get; } = [];

    /// <summary>Gets the set of currently selected countries.</summary>
    public HashSet<Country> SelectedCountries { get; } = [];

    /// <summary>Gets the active state map for all days of the week.</summary>
    public Dictionary<Weekdays, bool> DayActive { get; } = AllWeekdays.ToDictionary(d => d, _ => false);

    /// <summary>Gets the list of delivery time strings (HH:mm).</summary>
    public List<string> SharedTimes { get; } = ["10:00"];

    /// <summary>Gets or sets whether newsletter dispatch is active for this subscription.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Gets or sets whether a save request is in flight.</summary>
    public bool IsSaving { get; set; }

    /// <summary>Gets or sets user feedback or validation error messages.</summary>
    public string? NewsFeedback { get; set; }

    /// <summary>Gets summary text of selected categories for dropdown button.</summary>
    public string CategorySummaryText => SelectionSummary(SelectedCategories, NewsEnumGermanDisplayConstants.CategoryDe, "Noch keine Kategorie gewählt");

    /// <summary>Gets summary text of selected languages for dropdown button.</summary>
    public string LanguageSummaryText => SelectionSummary(SelectedLanguages, NewsEnumGermanDisplayConstants.LanguageDe, "Noch keine Sprache gewählt");

    /// <summary>Gets summary text of selected countries for dropdown button.</summary>
    public string CountrySummaryText => SelectionSummary(SelectedCountries, NewsEnumGermanDisplayConstants.CountryDe, "Noch kein Land gewählt");

    /// <summary>
    /// Populates the form fields from an existing subscription or resets to defaults for new creation.
    /// </summary>
    public void ApplyInitialModel(NewsSubscriptionDto? initialModel)
    {
        NewsFeedback = null;
        if (initialModel is null)
        {
            EditingId = 0;
            ResetEmptyForm();
            return;
        }

        EditingId = initialModel.Id;
        IsEnabled = initialModel.IsEnabled;
        KeywordsRaw = initialModel.Keywords is { Count: > 0 } ? string.Join(", ", initialModel.Keywords) : string.Empty;

        SelectedCategories.Clear();
        if (initialModel.Category is not null)
        {
            foreach (NewsCategory c in initialModel.Category)
                SelectedCategories.Add(c);
        }

        SelectedLanguages.Clear();
        if (initialModel.Languages is not null)
        {
            foreach (Language l in initialModel.Languages)
                SelectedLanguages.Add(l);
        }

        SelectedCountries.Clear();
        if (initialModel.Countries is not null)
        {
            foreach (Country c in initialModel.Countries)
                SelectedCountries.Add(c);
        }

        foreach (Weekdays d in AllWeekdays)
            DayActive[d] = false;
        foreach (Weekdays d in initialModel.SendOnWeekdays ?? [])
            DayActive[d] = true;

        SharedTimes.Clear();
        if (initialModel.SendAtTimes is { Count: > 0 } at)
        {
            foreach (TimeOnly t in at.OrderBy(x => x))
                SharedTimes.Add(t.ToString("HH:mm"));
        }
        else
        {
            SharedTimes.Add("10:00");
        }

        NotifyStateChanged();
    }

    /// <summary>
    /// Resets all form fields to default empty state.
    /// </summary>
    public void ResetEmptyForm()
    {
        KeywordsRaw = string.Empty;
        IsEnabled = true;
        SelectedCategories.Clear();
        SelectedLanguages.Clear();
        SelectedCountries.Clear();
        foreach (Weekdays d in AllWeekdays)
            DayActive[d] = false;
        SharedTimes.Clear();
        SharedTimes.Add("10:00");
        NotifyStateChanged();
    }

    /// <summary>Toggles selection of a specific news category.</summary>
    public void ToggleCategory(NewsCategory category, bool on)
    {
        if (on) SelectedCategories.Add(category);
        else SelectedCategories.Remove(category);
        NotifyStateChanged();
    }

    /// <summary>Toggles selection of a specific language.</summary>
    public void ToggleLanguage(Language language, bool on)
    {
        if (on) SelectedLanguages.Add(language);
        else SelectedLanguages.Remove(language);
        NotifyStateChanged();
    }

    /// <summary>Toggles selection of a specific country.</summary>
    public void ToggleCountry(Country country, bool on)
    {
        if (on) SelectedCountries.Add(country);
        else SelectedCountries.Remove(country);
        NotifyStateChanged();
    }

    /// <summary>Toggles active state for a given weekday.</summary>
    public void ToggleDay(Weekdays day)
    {
        DayActive[day] = !DayActive[day];
        NotifyStateChanged();
    }

    /// <summary>Updates the time string for a specific slot index.</summary>
    public void SetSharedTime(int index, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || index < 0 || index >= SharedTimes.Count)
            return;
        SharedTimes[index] = NormalizeTime(value);
        NotifyStateChanged();
    }

    /// <summary>Adds a new delivery time slot.</summary>
    public void AddSharedTimeSlot()
    {
        string template = SharedTimes.Count > 0 ? SharedTimes[^1] : "10:00";
        SharedTimes.Add(NormalizeTime(template));
        NotifyStateChanged();
    }

    /// <summary>Removes the delivery time slot at the specified index.</summary>
    public void RemoveSharedTimeSlot(int index)
    {
        if (SharedTimes.Count <= 1 || index < 0 || index >= SharedTimes.Count)
            return;
        SharedTimes.RemoveAt(index);
        NotifyStateChanged();
    }

    /// <summary>Normalizes raw time input into HH:mm format.</summary>
    public static string NormalizeTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "10:00";
        string s = raw.Trim();
        return s.Length >= 5 ? s[..5] : s;
    }

    /// <summary>
    /// Validates inputs and sends create or update requests to the API.
    /// </summary>
    public async Task<(bool success, int? createdId)> SaveNewsConfigurationAsync(CancellationToken cancellationToken = default)
    {
        NewsFeedback = null;
        await authTokens.EnsureLoadedFromStorageAsync(cancellationToken).ConfigureAwait(false);
        int? userId = authTokens.AccessToken.TryGetUserId();
        if (userId is null)
        {
            NewsFeedback = "Bitte erneut anmelden.";
            NotifyStateChanged();
            return (false, null);
        }

        List<string> keywords = KeywordsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0).ToList();

        List<Weekdays> weekdays = DayActive.Where(kv => kv.Value).Select(kv => kv.Key).OrderBy(d => (int)d).ToList();
        if (weekdays.Count == 0)
        {
            NewsFeedback = "Bitte mindestens einen Sendetag wählen.";
            NotifyStateChanged();
            return (false, null);
        }

        if (SharedTimes.Count == 0)
        {
            NewsFeedback = "Bitte mindestens eine Sendezeit angeben.";
            NotifyStateChanged();
            return (false, null);
        }

        List<TimeOnly> times = SharedTimes
            .Select(t => TimeOnly.ParseExact(NormalizeTime(t), "HH:mm", CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        IsSaving = true;
        NotifyStateChanged();

        try
        {
            if (EditingId <= 0)
            {
                CreateNewsletterSubscriptionPayloadDto createPayload = new()
                {
                    Keywords = keywords.Count > 0 ? keywords : null,
                    Category = SelectedCategories.Count > 0 ? SelectedCategories.ToList() : null,
                    Languages = SelectedLanguages.Count > 0 ? SelectedLanguages.ToList() : null,
                    Countries = SelectedCountries.Count > 0 ? SelectedCountries.ToList() : null,
                    SendOnWeekdays = weekdays,
                    SendAtTimes = times,
                    IsEnabled = IsEnabled,
                };

                using HttpResponseMessage response = await http.PostAsJsonAsync(
                    $"api/v1/users/{userId.Value}/newsletter-subscriptions",
                    createPayload,
                    HermesNewsJsonMapper.Options,
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var (msg, _, _) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                    NewsFeedback = msg;
                    return (false, null);
                }

                CreateNewsletterSubscriptionResponseDto? created = await response.Content.ReadFromJsonAsync<CreateNewsletterSubscriptionResponseDto>(
                    HermesNewsJsonMapper.Options,
                    cancellationToken).ConfigureAwait(false);

                NewsFeedback = created is not null
                    ? $"Gespeichert (Newsletter-Abonnement #{created.SubscriptionId})."
                    : "Gespeichert.";

                toastService.ShowSuccess(created is not null ? $"Newsletter-Abonnement #{created.SubscriptionId} gespeichert." : "Newsletter-Abonnement gespeichert.", "News");

                return (true, created?.SubscriptionId);
            }
            else
            {
                UpdateNewsletterSubscriptionPayloadDto updatePayload = new()
                {
                    Id = EditingId,
                    Keywords = keywords.Count > 0 ? keywords : null,
                    Category = SelectedCategories.Count > 0 ? SelectedCategories.ToList() : null,
                    Languages = SelectedLanguages.Count > 0 ? SelectedLanguages.ToList() : null,
                    Countries = SelectedCountries.Count > 0 ? SelectedCountries.ToList() : null,
                    SendOnWeekdays = weekdays,
                    SendAtTimes = times,
                    IsEnabled = IsEnabled,
                };

                using HttpResponseMessage response = await http.PutAsJsonAsync(
                    $"api/v1/users/{userId.Value}/newsletter-subscriptions/{EditingId}",
                    updatePayload,
                    HermesNewsJsonMapper.Options,
                    cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    var (msg, _, _) = await ApiResponseReader.ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
                    NewsFeedback = msg;
                    return (false, null);
                }

                NewsFeedback = "Änderungen gespeichert.";
                toastService.ShowSuccess("Änderungen am Newsletter-Abonnement gespeichert.", "News");
                return (true, EditingId);
            }
        }
        catch (Exception ex)
        {
            NewsFeedback = $"Speichern fehlgeschlagen: {ex.Message}";
            return (false, null);
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
