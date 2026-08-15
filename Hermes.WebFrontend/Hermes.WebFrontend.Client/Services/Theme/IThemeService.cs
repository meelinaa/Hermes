namespace Hermes.WebFrontend.Client.Services.Theme;

/// <summary>
/// Service interface managing application color theme state, persistence, and transitions.
/// </summary>
public interface IThemeService
{
    /// <summary>Gets the user's selected theme preference.</summary>
    AppTheme CurrentTheme { get; }

    /// <summary>Event raised whenever the active theme changes.</summary>
    event Action? ThemeChanged;

    /// <summary>Initializes theme state from storage or system preference.</summary>
    Task InitializeAsync();

    /// <summary>Changes the current theme and persists the selection.</summary>
    Task SetThemeAsync(AppTheme theme);

    /// <summary>Cycles to the next theme: Light -> Dark -> System -> Light.</summary>
    Task CycleThemeAsync();
}
