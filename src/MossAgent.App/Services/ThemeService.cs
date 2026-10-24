using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Media;
using MossAgent.Application.Settings;

using MossAgent.App.Resources;
namespace MossAgent.App.Services;

/// <summary>
/// 界面主题管理服务实现，支持明暗主题在本地存储中持久化并应用到 Avalonia 应用程序。
/// </summary>
public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly IAppearancePreferenceStore? _store;
    private readonly Avalonia.Application? _application;
    private ThemePreference _preference;
    private ThemePalette _lightPalette;
    private ThemePalette _darkPalette;

    public ThemeService(IAppearancePreferenceStore? store = null)
    {
        _store = store;
        _preference = store?.LoadTheme() ?? ThemePreference.System;
        _lightPalette = store?.LoadThemePalette(ThemePreference.Light) ?? ThemePalette.LightDefault;
        _darkPalette = store?.LoadThemePalette(ThemePreference.Dark) ?? ThemePalette.DarkDefault;
        _application = Avalonia.Application.Current;
        if (_application is not null) _application.PropertyChanged += OnApplicationPropertyChanged;
        ApplyTheme();
    }

    /// <inheritdoc />
    public bool IsDarkMode => Preference == ThemePreference.System
        ? Avalonia.Application.Current?.ActualThemeVariant == ThemeVariant.Dark
        : Preference == ThemePreference.Dark;

    public ThemePreference Preference => _preference;

    public string? PersistenceError { get; private set; }

    /// <inheritdoc />
    public event Action? ThemeChanged;

    public void Dispose()
    {
        if (_application is not null) _application.PropertyChanged -= OnApplicationPropertyChanged;
    }

    /// <inheritdoc />
    public void ToggleTheme()
    {
        SetPreference(IsDarkMode ? ThemePreference.Light : ThemePreference.Dark);
    }

    public void SetPreference(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference)) return;
        _preference = preference;
        try
        {
            _store?.SaveTheme(preference);
            PersistenceError = null;
        }
        catch
        {
            PersistenceError = UiText.T("主题已应用，但保存失败。请检查应用数据目录的写入权限。");
        }

        ApplyTheme();
    }

    public ThemePalette PaletteFor(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => _lightPalette,
        ThemePreference.Dark => _darkPalette,
        _ => IsDarkMode ? _darkPalette : _lightPalette
    };

    public void SetPalette(ThemePreference theme, ThemePalette? palette)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        var applied = palette ?? (theme == ThemePreference.Dark
            ? ThemePalette.DarkDefault : ThemePalette.LightDefault);
        if (theme == ThemePreference.Dark) _darkPalette = applied;
        else _lightPalette = applied;
        try
        {
            _store?.SaveThemePalette(theme, palette);
            PersistenceError = null;
        }
        catch
        {
            PersistenceError = UiText.T("主题颜色已应用，但保存失败。请检查应用数据目录的写入权限。");
        }

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var variant = ToVariant(_preference);
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current is { } application)
            {
                application.RequestedThemeVariant = variant;
                ApplyPalette(application, ThemeVariant.Light, _lightPalette);
                ApplyPalette(application, ThemeVariant.Dark, _darkPalette);
            }
            ThemeChanged?.Invoke();
        });
    }

    private void OnApplicationPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (_preference == ThemePreference.System
            && args.Property.Name == nameof(Avalonia.Application.ActualThemeVariant))
            ThemeChanged?.Invoke();
    }

    private static void ApplyPalette(Avalonia.Application application, ThemeVariant variant,
        ThemePalette palette)
    {
        SetBrush("AppBackgroundBrush", palette.Background);
        SetBrush("TextPrimaryBrush", palette.Foreground);
        SetBrush("PrimaryButtonBackgroundBrush", palette.AccentHex
            ?? (variant == ThemeVariant.Dark ? "#F4F4F6" : ThemePalette.LightDefault.Foreground));
        SetBrush("PrimaryButtonForegroundBrush", palette.AccentHex is null
            && variant == ThemeVariant.Dark ? "#0A0A0C" : "#FFFFFF");

        void SetBrush(string key, string hex)
        {
            if (application.TryGetResource(key, variant, out var value)
                && value is SolidColorBrush brush)
                brush.Color = Color.Parse(hex);
        }
    }

    private static ThemeVariant ToVariant(ThemePreference preference) => preference switch
    {
        ThemePreference.Dark => ThemeVariant.Dark,
        ThemePreference.System => ThemeVariant.Default,
        _ => ThemeVariant.Light
    };
}

