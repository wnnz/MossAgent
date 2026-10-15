namespace MossAgent.Application.Settings;

public interface IAppearancePreferenceStore
{
    ThemePreference LoadTheme();

    void SaveTheme(ThemePreference preference);

    ThemePalette? LoadThemePalette(ThemePreference theme);

    void SaveThemePalette(ThemePreference theme, ThemePalette? palette);

    CodeFontPreference LoadCodeFont();

    void SaveCodeFont(CodeFontPreference preference);

    CodeFontPreference? LoadCodeFont(ThemePreference theme);

    void SaveCodeFont(ThemePreference theme, CodeFontPreference preference);

    UiFontPreference LoadUiFont();

    void SaveUiFont(UiFontPreference preference);

    UiFontPreference? LoadUiFont(ThemePreference theme);

    void SaveUiFont(ThemePreference theme, UiFontPreference preference);

    ContentFontPreference? LoadContentFont(ThemePreference theme);

    void SaveContentFont(ThemePreference theme, ContentFontPreference preference);
}
