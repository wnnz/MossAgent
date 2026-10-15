using Avalonia.Styling;
using Avalonia.Threading;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.App.Tests;

internal sealed class ThemePaletteChoiceFixture : IDisposable
{
    private readonly ThemeService _themeService;
    private readonly UiFontService _uiFonts;
    private readonly ContentFontService _contentFonts;
    private readonly CodeFontService _codeFonts;

    public ThemePaletteChoiceFixture(bool dark, double width = 384)
    {
        Root = Path.Combine(Path.GetTempPath(), "mossagent-palette-choice", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Store = new JsonAppearancePreferenceStore(new AppDataPaths(Root));
        Theme = dark ? ThemePreference.Dark : ThemePreference.Light;
        _themeService = new ThemeService(Store);
        _themeService.SetPreference(Theme);
        _uiFonts = new UiFontService(_themeService, Store);
        _contentFonts = new ContentFontService(_themeService, _uiFonts, Store);
        _codeFonts = new CodeFontService(_themeService, Store);
        var settings = new ThemePaletteSettingsViewModel(_themeService, _uiFonts, _contentFonts, _codeFonts);
        Editor = dark ? settings.Dark : settings.Light;
        Card = new AppThemePaletteCard { DataContext = Editor };
        Window = new AppWindow
        {
            Width = width,
            Height = 320,
            Content = Card,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Flush();
    }

    public string Root { get; }
    public JsonAppearancePreferenceStore Store { get; }
    public ThemePreference Theme { get; }
    public ThemePaletteEditorViewModel Editor { get; }
    public AppThemePaletteCard Card { get; }
    public AppWindow Window { get; }

    public void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    public void Dispose()
    {
        Window.Close();
        _contentFonts.Dispose();
        _codeFonts.Dispose();
        _uiFonts.Dispose();
        _themeService.Dispose();
        Directory.Delete(Root, recursive: true);
    }
}
