namespace MossAgent.App.Resources;

public static class AppearanceText
{
    public static string Mode => SettingsText.Get("AppearanceMode");
    public static string VisualStyle => SettingsText.Get("AppearanceVisualStyle");
    public static string Advanced => SettingsText.Get("AppearanceAdvanced");
    public static string Theme => SettingsText.Get("AppearanceTheme");
    public static string Font => SettingsText.Get("AppearanceFont");
    public static string BackgroundColorName => SettingsText.Get("AppearanceBackgroundColorName");
    public static string ForegroundColorName => SettingsText.Get("AppearanceForegroundColorName");
    public static string AccentColorName => SettingsText.Get("AppearanceAccentColorName");
    public static string UiFontName => SettingsText.Get("AppearanceUiFontName");
    public static string ThemePresetName => SettingsText.Get("AppearanceThemePresetName");
    public static string DefaultTheme => SettingsText.Get("AppearanceDefaultTheme");
    public static string CustomTheme => SettingsText.Get("AppearanceCustomTheme");
    public static string ThemeApplied => SettingsText.Get("AppearanceThemeApplied");
    public static string ApplyColors => SettingsText.Get("AppearanceApplyColors");
    public static string ResetColors => SettingsText.Get("AppearanceResetColors");
}
