using MossAgent.Application.Settings;

namespace MossAgent.App.Services;

/// <summary>
/// 界面主题管理服务接口，提供明暗主题查询、切换与状态通知。
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 当前是否处于暗色（Dark）主题模式。
    /// </summary>
    bool IsDarkMode { get; }

    ThemePreference Preference { get; }

    string? PersistenceError { get; }

    /// <summary>
    /// 当主题发生变化时触发的事件。
    /// </summary>
    event Action? ThemeChanged;

    /// <summary>
    /// 在明暗主题之间进行切换。
    /// </summary>
    void ToggleTheme();

    void SetPreference(ThemePreference preference);

    ThemePalette PaletteFor(ThemePreference theme);

    void SetPalette(ThemePreference theme, ThemePalette? palette);
}
