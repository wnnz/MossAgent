using MossAgent.App.Services;
using MossAgent.Application.Settings;

namespace MossAgent.App.Tests;

internal sealed class ThemeChangeProbeService : IThemeService
{
    public bool IsDarkMode => Preference == ThemePreference.Dark;
    public ThemePreference Preference { get; private set; } = ThemePreference.Light;
    public string? PersistenceError { get; set; }
    public event Action? ThemeChanged;
    public int SubscriberCount => ThemeChanged?.GetInvocationList().Length ?? 0;
    public Action? PendingNotification => ThemeChanged;

    public void ToggleTheme() => SetPreference(IsDarkMode ? ThemePreference.Light : ThemePreference.Dark);

    public void SetPreference(ThemePreference preference)
    {
        Preference = preference;
        ThemeChanged?.Invoke();
    }

    public ThemePalette PaletteFor(ThemePreference theme) => theme == ThemePreference.Dark
        ? ThemePalette.DarkDefault : ThemePalette.LightDefault;

    public void SetPalette(ThemePreference theme, ThemePalette? palette) => ThemeChanged?.Invoke();
}
