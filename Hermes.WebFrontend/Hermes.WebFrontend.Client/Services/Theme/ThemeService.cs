using Microsoft.JSInterop;

namespace Hermes.WebFrontend.Client.Services.Theme;

/// <summary>
/// Manages application color theme state, coordinates with JS interop, and notifies UI listeners.
/// </summary>
public sealed class ThemeService(IJSRuntime jsRuntime) : IThemeService
{
    /// <inheritdoc />
    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    /// <inheritdoc />
    public event Action? ThemeChanged;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            string? raw = await jsRuntime.InvokeAsync<string>("hermesTheme.getTheme").ConfigureAwait(false);
            CurrentTheme = ParseTheme(raw);
            ThemeChanged?.Invoke();
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    public async Task SetThemeAsync(AppTheme theme)
    {
        CurrentTheme = theme;
        try
        {
            string themeKey = theme.ToString().ToLowerInvariant();
            await jsRuntime.InvokeVoidAsync("hermesTheme.setTheme", themeKey).ConfigureAwait(false);
        }
        catch
        {
        }
        ThemeChanged?.Invoke();
    }

    /// <inheritdoc />
    public async Task CycleThemeAsync()
    {
        AppTheme nextTheme = CurrentTheme switch
        {
            AppTheme.System => AppTheme.Light,
            AppTheme.Light => AppTheme.Dark,
            AppTheme.Dark => AppTheme.System,
            _ => AppTheme.System
        };
        await SetThemeAsync(nextTheme).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses a string representation into an <see cref="AppTheme"/> enum value.
    /// </summary>
    /// <param name="raw">The raw string key (e.g. "light", "dark", "system").</param>
    /// <returns>The resolved <see cref="AppTheme"/>.</returns>
    public static AppTheme ParseTheme(string? raw)
    {
        return raw?.ToLowerInvariant() switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.System
        };
    }
}
