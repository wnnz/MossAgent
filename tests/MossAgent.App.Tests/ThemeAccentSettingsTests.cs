using System.Xml.Linq;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ThemeAccentSettingsTests
{
    [Theory]
    [InlineData("#FFFF00")]
    [InlineData("#GGGGGG")]
    [InlineData("#12345678")]
    public void InvalidOrLowContrastAccentIsRejected(string accent)
    {
        Assert.False(ThemePalette.TryCreate("#FFFFFF", "#0D0D0D", accent, out var palette));
        Assert.Null(palette);
    }

    [Fact]
    public void LegacyPaletteWithoutAccentStillLoads()
    {
        var root = NewRoot();
        try
        {
            File.WriteAllText(Path.Combine(root, "ui-settings.json"),
                """{"ThemePalettes":{"Light":{"Background":"#EFF3F7","Foreground":"#202A31"}}}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));

            var palette = store.LoadThemePalette(ThemePreference.Light);

            Assert.NotNull(palette);
            Assert.Equal("#EFF3F7", palette.Background);
            Assert.Null(palette.AccentHex);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void SelectingAccentAppliesPersistsAndResets(ThemePreference theme)
    {
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);
            service.SetPreference(theme);
            var settings = CreateSettings(service, store);
            var editor = theme == ThemePreference.Light ? settings.Light : settings.Dark;
            var blue = Assert.Single(editor.AccentOptions, option => option.Value == "#2563EB");

            editor.SelectedAccentOption = blue;

            Assert.Equal("#2563EB", service.PaletteFor(theme).AccentHex);
            Assert.Equal("#2563EB", store.LoadThemePalette(theme)?.AccentHex);
            Assert.Equal("#2563EB", new ThemeService(store).PaletteFor(theme).AccentHex);
            Assert.False(editor.HasError);

            editor.ResetCommand.Execute(null);

            Assert.Null(store.LoadThemePalette(theme));
            Assert.Null(editor.SelectedAccentOption?.Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidSelectionKeepsPreviousAccentAndSavedData()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            var service = new ThemeService(store);
            service.SetPreference(ThemePreference.Light);
            var editor = CreateSettings(service, store).Light;
            var blue = Assert.Single(editor.AccentOptions, option => option.Value == "#2563EB");
            editor.SelectedAccentOption = blue;
            var saved = File.ReadAllText(path);

            foreach (var invalid in new[] { "#FFFF00", "#GGGGGG" })
            {
                editor.SelectedAccentOption = new ThemeAccentOption(invalid, "无效颜色");

                Assert.Same(blue, editor.SelectedAccentOption);
                Assert.True(editor.HasError);
                Assert.Equal("#2563EB", service.PaletteFor(ThemePreference.Light).AccentHex);
                Assert.Equal(saved, File.ReadAllText(path));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AppearanceUsesSharedAccentSelectorAndSwatch()
    {
        var root = RepositoryRoot();
        var view = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views",
            "AppearanceSettingsPane.axaml"));
        var styles = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles",
            "ThemePaletteVariants.axaml"));

        var card = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls",
            "AppThemePaletteCard.axaml"));
        Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppThemePaletteCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette}");
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppPillComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedAccentOption}");
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppColorSwatch"
            && element.Attribute("ColorHex")?.Value == "{Binding $parent[ui:AppThemePaletteCard].DataContext.AccentHex}");
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppColorSwatch.theme-color-swatch");
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
        var root = Path.Combine(Path.GetTempPath(), "mossagent-accent-test", Guid.NewGuid().ToString("N"));
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
