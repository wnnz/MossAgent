using MossAgent.App.Services;
using MossAgent.Application.Settings;

using MossAgent.App.Resources;
namespace MossAgent.App.ViewModels;

public sealed class ThemePaletteSettingsViewModel
{
    public ThemePaletteSettingsViewModel(IThemeService service, IUiFontService uiFonts,
        IContentFontService contentFonts, ICodeFontService codeFonts)
    {
        Light = new ThemePaletteEditorViewModel(service, ThemePreference.Light, UiText.T("浅色主题"),
            new ThemeFontEditorViewModel(ThemePreference.Light, uiFonts, contentFonts, codeFonts));
        Dark = new ThemePaletteEditorViewModel(service, ThemePreference.Dark, UiText.T("深色主题"),
            new ThemeFontEditorViewModel(ThemePreference.Dark, uiFonts, contentFonts, codeFonts));
    }

    public ThemePaletteEditorViewModel Light { get; }

    public ThemePaletteEditorViewModel Dark { get; }
}

