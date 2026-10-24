using Avalonia.Media;
using Avalonia.Threading;
using MossAgent.Application.Settings;

using MossAgent.App.Resources;
namespace MossAgent.App.Services;

public sealed class ContentFontService : IContentFontService, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly IUiFontService _uiFonts;
    private readonly IAppearancePreferenceStore? _store;
    private ContentFontPreference _lightPreference;
    private ContentFontPreference _darkPreference;

    public ContentFontService(IThemeService themeService, IUiFontService uiFonts,
        IAppearancePreferenceStore? store = null)
    {
        _themeService = themeService;
        _uiFonts = uiFonts;
        _store = store;
        _lightPreference = store?.LoadContentFont(ThemePreference.Light)
            ?? ContentFontPreference.SameAsUi;
        _darkPreference = store?.LoadContentFont(ThemePreference.Dark)
            ?? ContentFontPreference.SameAsUi;
        _uiFonts.Changed += Apply;
        Apply();
    }

    public string FamilyName => PreferenceFor(_themeService.IsDarkMode
        ? ThemePreference.Dark : ThemePreference.Light) switch
    {
        ContentFontPreference.Inter => "fonts:Inter#Inter",
        ContentFontPreference.SegoeUi => "Segoe UI",
        _ => _uiFonts.FamilyName
    };

    public string? PersistenceError { get; private set; }

    public ContentFontPreference PreferenceFor(ThemePreference theme) => theme switch
    {
        ThemePreference.Light => _lightPreference,
        ThemePreference.Dark => _darkPreference,
        _ => throw new ArgumentOutOfRangeException(nameof(theme))
    };

    public void SetPreference(ThemePreference theme, ContentFontPreference preference)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        if (!Enum.IsDefined(preference)) return;

        if (theme == ThemePreference.Dark) _darkPreference = preference;
        else _lightPreference = preference;
        try
        {
            _store?.SaveContentFont(theme, preference);
            PersistenceError = null;
        }
        catch
        {
            PersistenceError = UiText.T("内容字体已应用，但保存失败。请检查应用数据目录的写入权限。");
        }

        if (_themeService.IsDarkMode == (theme == ThemePreference.Dark)) Apply();
    }

    public void Dispose() => _uiFonts.Changed -= Apply;

    private void Apply()
    {
        var family = new FontFamily(FamilyName);
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current is not null)
                Avalonia.Application.Current.Resources["ContentFontFamily"] = family;
        });
    }
}

