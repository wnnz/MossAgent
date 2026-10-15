using MossAgent.App.Services;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

public sealed class ThemePaletteSettingsViewModel
{
    public ThemePaletteSettingsViewModel(IThemeService service, IUiFontService uiFonts,
        IContentFontService contentFonts, ICodeFontService codeFonts)
    {
        Light = new ThemePaletteEditorViewModel(service, ThemePreference.Light, "浅色主题",
            new ThemeFontEditorViewModel(ThemePreference.Light, uiFonts, contentFonts, codeFonts));
        Dark = new ThemePaletteEditorViewModel(service, ThemePreference.Dark, "深色主题",
            new ThemeFontEditorViewModel(ThemePreference.Dark, uiFonts, contentFonts, codeFonts));
    }

    public ThemePaletteEditorViewModel Light { get; }

    public ThemePaletteEditorViewModel Dark { get; }
}
