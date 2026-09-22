using System.Text.Json;
using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.App.Services;

/// <summary>
/// 界面主题管理服务实现，支持明暗主题在本地存储中持久化并应用到 Avalonia 应用程序。
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly string? _settingsFilePath;
    private bool _isDarkMode = true;

    public ThemeService(AppDataPaths? appDataPaths = null)
    {
        if (appDataPaths is not null)
        {
            _settingsFilePath = Path.Combine(appDataPaths.Root, "ui-settings.json");
            LoadSavedTheme();
        }
        else if (Avalonia.Application.Current is not null)
        {
            _isDarkMode = Avalonia.Application.Current.RequestedThemeVariant != ThemeVariant.Light;
        }
    }

    /// <inheritdoc />
    public bool IsDarkMode => _isDarkMode;

    /// <inheritdoc />
    public event Action? ThemeChanged;

    /// <inheritdoc />
    public void ToggleTheme()
    {
        _isDarkMode = !_isDarkMode;
        var nextVariant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;

        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current is not null)
            {
                Avalonia.Application.Current.RequestedThemeVariant = nextVariant;
            }
            ThemeChanged?.Invoke();
        });

        SaveTheme();
    }

    private void LoadSavedTheme()
    {
        try
        {
            if (_settingsFilePath is not null && File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Theme", out var themeProp))
                {
                    var saved = themeProp.GetString();
                    _isDarkMode = !string.Equals(saved, "Light", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch
        {
            _isDarkMode = true;
        }

        var variant = _isDarkMode ? ThemeVariant.Dark : ThemeVariant.Light;
        Dispatcher.UIThread.Post(() =>
        {
            if (Avalonia.Application.Current is not null)
            {
                Avalonia.Application.Current.RequestedThemeVariant = variant;
            }
            ThemeChanged?.Invoke();
        });
    }

    private void SaveTheme()
    {
        if (_settingsFilePath is null) return;
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { Theme = _isDarkMode ? "Dark" : "Light" });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // 忽略写入失败，保证界面交互无阻
        }
    }
}
