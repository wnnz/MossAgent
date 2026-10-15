using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Media;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePaletteSettingsTests
{
    [Theory]
    [InlineData("#FFFFFF", "#FFFFFF")]
    [InlineData("#GGGGGG", "#000000")]
    [InlineData("#FFFFFF00", "#000000")]
    [InlineData("#888888", "#999999")]
    public void InvalidOrUnreadableColorsAreRejected(string background, string foreground)
    {
        Assert.False(ThemePalette.TryCreate(background, foreground, out var palette));
        Assert.Null(palette);
    }

    [Fact]
    public void DefaultPaletteMatchesApplicationThemeResources()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        foreach (var (themeName, palette) in new[]
        {
            ("Light", ThemePalette.LightDefault), ("Dark", ThemePalette.DarkDefault)
        })
        {
            var theme = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "ResourceDictionary"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                    && attribute.Value == themeName));
            var brushes = theme.Elements().Where(element => element.Name.LocalName == "SolidColorBrush")
                .ToDictionary(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value,
                    element => element.Attribute("Color")!.Value);
            Assert.Equal(palette.Background, brushes["AppBackgroundBrush"]);
            Assert.Equal(palette.Foreground, brushes["TextPrimaryBrush"]);
        }
    }

    [Fact]
    public void NewDefaultDoesNotReplaceSavedLightForeground()
    {
        Assert.Equal("#1A1C1F", ThemePalette.LightDefault.Foreground);
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            Assert.True(ThemePalette.TryCreate("#FFFFFF", "#0D0D0D", out var saved));
            store.SaveThemePalette(ThemePreference.Light, saved);
            var path = Path.Combine(root, "ui-settings.json");
            var original = File.ReadAllText(path);
            using var service = new ThemeService(store);

            Assert.Equal(saved, service.PaletteFor(ThemePreference.Light));
            Assert.Equal(original, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AppearanceShowsTheActiveReusablePaletteAndRetainsExtendedFontsInAdvanced()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var view = XDocument.Load(Path.Combine(appPath, "Views", "AppearanceSettingsPane.axaml"));
        var card = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemePaletteCard.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "ThemePaletteVariants.axaml"));
        Assert.Equal(["{Binding ActivePalette}"],
            view.Descendants().Where(element => element.Name.LocalName == "AppThemePaletteCard")
                .Select(element => element.Attribute("DataContext")?.Value));
        Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppThemeExtendedFontsCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette.Fonts}"
            && element.Ancestors().Any(parent => parent.Name.LocalName == "AppExpander"));
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppCard"
            && element.Attribute("AutomationProperties.Name")?.Value == "{Binding ThemeTitle}");
        Assert.Single(card.Descendants(), element => element.Name.LocalName == "AppPillComboBox"
            && element.Attribute("ItemsSource")?.Value == "{Binding ThemeOptions}"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedThemeOption}"
            && element.Attribute("AutomationProperties.Name")?.Value == "{x:Static resources:AppearanceText.ThemePresetName}");
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppText"
            && element.Attribute("Text")?.Value == "{x:Static resources:AppearanceText.Font}");
        foreach (var field in new[] { "BackgroundHex", "ForegroundHex" })
            Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppThemeColorInput"
                && element.Attribute("Text")?.Value == $"{{Binding {field}, Mode=TwoWay}}"
                && element.Attribute("Label") is not null);
        foreach (var action in new[] { "SaveCommand", "ResetCommand" })
            Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppIconButton"
                && element.Attribute("Command")?.Value == $"{{Binding {action}}}"
                && element.Attribute("Label") is not null
                && element.Attribute("IconData") is not null
                && element.Attribute("Classes")?.Value.Split(' ').Contains("theme-color-action") == true);
        Assert.Equal(1, card.Descendants().Count(element => element.Name.LocalName == "AppColorSwatch"
            && element.Attribute("ColorHex") is not null));
        var colorInput = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemeColorInput.axaml"));
        Assert.Single(colorInput.Descendants(), element => element.Name.LocalName == "AppColorSwatch"
            && element.Attribute("ColorHex")?.Value == "{Binding Text, ElementName=Root}");
        Assert.Single(colorInput.Descendants(), element => element.Name.LocalName == "AppInput"
            && element.Attribute("Text")?.Value == "{Binding Text, ElementName=Root, Mode=TwoWay}"
            && element.Attribute("AutomationProperties.Name")?.Value == "{Binding Label, ElementName=Root}");
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppColorSwatch.theme-color-swatch");
    }

    [Fact]
    public void PalettesPersistSeparatelyWithoutLosingOtherSettings()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"Theme":"Light","CodeFont":"Consolas","Other":42}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            Assert.True(ThemePalette.TryCreate("#EFF3F7", "#202A31", out var light));
            Assert.True(ThemePalette.TryCreate("#202020", "#F0F0F0", out var dark));

            store.SaveThemePalette(ThemePreference.Light, light);
            store.SaveThemePalette(ThemePreference.Dark, dark);
            Assert.Equal(light, store.LoadThemePalette(ThemePreference.Light));
            Assert.Equal(dark, store.LoadThemePalette(ThemePreference.Dark));
            store.SaveThemePalette(ThemePreference.Light, null);

            Assert.Null(store.LoadThemePalette(ThemePreference.Light));
            Assert.Equal(dark, store.LoadThemePalette(ThemePreference.Dark));
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
    public void InvalidExistingPaletteDataIsNotOverwritten()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"ThemePalettes":"opaque"}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);

            service.SetPalette(ThemePreference.Light, ThemePalette.LightDefault);

            Assert.NotNull(service.PersistenceError);
            Assert.Equal("""{"ThemePalettes":"opaque"}""", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void EditorRejectsInvalidInputAndCanResetSavedColors(ThemePreference theme)
    {
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);
            service.SetPreference(theme);
            var settings = CreateSettings(service, store);
            var editor = theme == ThemePreference.Light ? settings.Light : settings.Dark;
            editor.BackgroundHex = "#FFFFFF";
            editor.ForegroundHex = "#FFFFFF";
            editor.SaveCommand.Execute(null);
            Assert.True(editor.HasError);
            Assert.Null(store.LoadThemePalette(theme));

            editor.BackgroundHex = "#EFF3F7";
            editor.ForegroundHex = "#202A31";
            editor.SaveCommand.Execute(null);
            Assert.False(editor.HasError);
            Assert.Equal("#EFF3F7", store.LoadThemePalette(theme)?.Background);

            editor.ResetCommand.Execute(null);
            Assert.Null(store.LoadThemePalette(theme));
            Assert.Equal(theme == ThemePreference.Light ? ThemePalette.LightDefault.Background
                : ThemePalette.DarkDefault.Background, editor.BackgroundHex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EditorsPreserveDraftsAcrossThemeSwitchAndPersistIndependently()
    {
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);
            var settings = CreateSettings(service, store);
            settings.Light.BackgroundHex = "#EFF3F7";
            settings.Light.ForegroundHex = "#202A31";
            settings.Dark.BackgroundHex = "#202020";
            settings.Dark.ForegroundHex = "#F0F0F0";

            service.SetPreference(ThemePreference.Dark);
            service.SetPreference(ThemePreference.Light);

            Assert.Equal("#EFF3F7", settings.Light.BackgroundHex);
            Assert.Equal("#202020", settings.Dark.BackgroundHex);
            settings.Light.SaveCommand.Execute(null);
            settings.Dark.SaveCommand.Execute(null);
            Assert.Equal("#EFF3F7", store.LoadThemePalette(ThemePreference.Light)?.Background);
            Assert.Equal("#202020", store.LoadThemePalette(ThemePreference.Dark)?.Background);

            settings.Light.ResetCommand.Execute(null);
            Assert.Null(store.LoadThemePalette(ThemePreference.Light));
            Assert.Equal("#202020", store.LoadThemePalette(ThemePreference.Dark)?.Background);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EditorReportsPersistenceFailureWithoutClaimingSaved()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"ThemePalettes":"opaque"}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);
            var settings = CreateSettings(service, store);
            settings.Light.BackgroundHex = "#EFF3F7";
            settings.Light.ForegroundHex = "#202A31";

            settings.Light.SaveCommand.Execute(null);

            Assert.True(settings.Light.HasError);
            Assert.False(settings.Light.HasStatus);
            Assert.Equal("""{"ThemePalettes":"opaque"}""", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ColorSwatchRendersValidHexAndRejectsInvalidDrafts()
    {
        var swatch = new AppColorSwatch { ColorHex = "#2563EB" };
        Assert.Equal(Color.Parse("#2563EB"), Assert.IsType<SolidColorBrush>(swatch.Background).Color);

        swatch.ColorHex = "not-a-color";
        Assert.Same(Brushes.Transparent, swatch.Background);

        swatch.ColorHex = "Red";
        Assert.Same(Brushes.Transparent, swatch.Background);
    }

    private static ThemePaletteSettingsViewModel CreateSettings(ThemeService theme,
        JsonAppearancePreferenceStore store)
    {
        var uiFonts = new UiFontService(theme, store);
        return new ThemePaletteSettingsViewModel(theme, uiFonts,
            new ContentFontService(theme, uiFonts, store), new CodeFontService(theme, store));
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-palette-test", Guid.NewGuid().ToString("N"));
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
