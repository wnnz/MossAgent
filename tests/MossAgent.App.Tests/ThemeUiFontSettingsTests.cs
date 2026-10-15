using System.Text.Json;
using System.Xml.Linq;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ThemeUiFontSettingsTests
{
    [Fact]
    public void LegacyFontIsInheritedUntilEachThemeIsCustomized()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path,
                """{"Theme":"Dark","UiFont":"SegoeUi","CodeFont":"Consolas","Other":42}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var theme = new ThemeService(store);
            using var fonts = new UiFontService(theme, store);
            var settings = CreateSettings(theme, fonts, store);
            Assert.Equal(UiFontPreference.SegoeUi, settings.Light.Fonts.SelectedUiFontOption.Value);
            Assert.Equal(UiFontPreference.SegoeUi, settings.Dark.Fonts.SelectedUiFontOption.Value);

            settings.Light.Fonts.SelectedUiFontOption = Assert.Single(settings.Light.Fonts.UiFontOptions,
                option => option.Value == UiFontPreference.Default);

            Assert.Equal(UiFontPreference.Default, store.LoadUiFont(ThemePreference.Light));
            Assert.Null(store.LoadUiFont(ThemePreference.Dark));
            Assert.Equal(UiFontPreference.SegoeUi, store.LoadUiFont());
            Assert.Equal("Segoe UI", fonts.FamilyName);
            theme.SetPreference(ThemePreference.Light);
            Assert.Equal("Segoe UI", fonts.FamilyName);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Consolas", document.RootElement.GetProperty("CodeFont").GetString());
            Assert.Equal(42, document.RootElement.GetProperty("Other").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ThemeFontsPersistIndependentlyAndReload()
    {
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var theme = new ThemeService(store);
            using (var fonts = new UiFontService(theme, store))
            {
                fonts.SetPreference(ThemePreference.Light, UiFontPreference.SegoeUi);
                fonts.SetPreference(ThemePreference.Dark, UiFontPreference.Default);
                Assert.Null(fonts.PersistenceError);
            }

            using var reloaded = new UiFontService(theme, store);
            Assert.Equal(UiFontPreference.SegoeUi, reloaded.PreferenceFor(ThemePreference.Light));
            Assert.Equal(UiFontPreference.Default, reloaded.PreferenceFor(ThemePreference.Dark));
            Assert.Equal(UiFontPreference.SegoeUi, store.LoadUiFont(ThemePreference.Light));
            Assert.Equal(UiFontPreference.Default, store.LoadUiFont(ThemePreference.Dark));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedThemeFontsStayIntactAndShowSaveFailure()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"ThemeUiFonts":"opaque"}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var theme = new ThemeService(store);
            using var fonts = new UiFontService(theme, store);
            var settings = CreateSettings(theme, fonts, store);

            settings.Light.Fonts.SelectedUiFontOption = Assert.Single(settings.Light.Fonts.UiFontOptions,
                option => option.Value == UiFontPreference.SegoeUi);

            Assert.True(settings.Light.Fonts.HasError);
            Assert.False(settings.Light.Fonts.HasStatus);
            Assert.Equal("""{"ThemeUiFonts":"opaque"}""", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidPreferenceCannotReplaceSavedFont()
    {
        var theme = new ThemeService();
        using var fonts = new UiFontService(theme);

        fonts.SetPreference(ThemePreference.Light, (UiFontPreference)99);

        Assert.Equal(UiFontPreference.Default, fonts.PreferenceFor(ThemePreference.Light));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fonts.SetPreference(ThemePreference.System, UiFontPreference.SegoeUi));
    }

    [Fact]
    public void AppearanceCardsBindThemeFontsToInheritedResource()
    {
        var appDirectory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var app = XDocument.Load(Path.Combine(appDirectory, "App.axaml"));
        var appearance = XDocument.Load(Path.Combine(appDirectory, "Views", "AppearanceSettingsPane.axaml"));
        var card = XDocument.Load(Path.Combine(appDirectory, "Controls", "AppThemePaletteCard.axaml"));

        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "FontFamily"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "UiFontFamily"));
        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Window"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "FontFamily"
                && setter.Attribute("Value")?.Value == "{DynamicResource UiFontFamily}"));
        Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppThemePaletteCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette}");
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppPillComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding Fonts.SelectedUiFontOption}");
    }

    private static ThemePaletteSettingsViewModel CreateSettings(ThemeService theme,
        UiFontService uiFonts, JsonAppearancePreferenceStore store) =>
        new(theme, uiFonts, new ContentFontService(theme, uiFonts, store),
            new CodeFontService(theme, store));

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-ui-font-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
