using Avalonia.Media;
using Avalonia.Threading;
using MossAgent.Application.Settings;

namespace MossAgent.App.Services;

public sealed class CodeFontService : ICodeFontService, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly IAppearancePreferenceStore? _store;
    private CodeFontPreference _lightPreference;
    private CodeFontPreference _darkPreference;

    public CodeFontService(IThemeService themeService, IAppearancePreferenceStore? store = null)
    {
        _themeService = themeService;
        _store = store;
        var legacyPreference = store?.LoadCodeFont() ?? CodeFontPreference.System;
        _lightPreference = store?.LoadCodeFont(ThemePreference.Light) ?? legacyPreference;
        _darkPreference = store?.LoadCodeFont(ThemePreference.Dark) ?? legacyPreference;
        _themeService.ThemeChanged += Apply;
        Apply();
    }

    public string FamilyName => PreferenceFor(_themeService.IsDarkMode
        ? ThemePreference.Dark : ThemePreference.Light) switch
    {
        CodeFontPreference.Consolas => "Consolas",
        CodeFontPreference.CascadiaCode => "Cascadia Code, Cascadia Mono, Consolas",
        _ => "Consolas, Cascadia Code, Courier New"
    };

    public string? PersistenceError { get; private set; }

    public CodeFontPreference PreferenceFor(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => _lightPreference,
        ThemePreference.Dark => _darkPreference,
        _ => throw new ArgumentOutOfRangeException(nameof(theme))
    };

    public void SetPreference(ThemePreference theme, CodeFontPreference preference)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        if (!Enum.IsDefined(preference)) return;

        if (theme == ThemePreference.Dark) _darkPreference = preference;
        else _lightPreference = preference;
        try
        {
            _store?.SaveCodeFont(theme, preference);
            PersistenceError = null;
        }
        catch
        {
            PersistenceError = "代码字体已应用，但保存失败。请检查应用数据目录的写入权限。";
        }

        if (_themeService.IsDarkMode == (theme == ThemePreference.Dark)) Apply();
    }

    public void Dispose() => _themeService.ThemeChanged -= Apply;

    private void Apply()
    {
        var family = new FontFamily(FamilyName);
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current is not null)
                Avalonia.Application.Current.Resources["CodeFontFamily"] = family;
        });
    }
}
