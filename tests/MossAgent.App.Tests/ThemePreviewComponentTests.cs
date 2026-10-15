using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ThemePreviewComponentTests
{
    [Theory]
    [InlineData("AppThemePreviewScene", "app-theme-preview-scene", "preview-backdrop")]
    [InlineData("AppDiffPreview", "app-diff-preview", "diff-preview-surface")]
    public void PreviewStylesUseClassesThatMatchUserControlWrappers(
        string component, string hostClass, string surfaceClass)
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var markup = XDocument.Load(Path.Combine(appPath, "Controls", $"{component}.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles",
            component == "AppDiffPreview" ? "ThemePreviewVariants.axaml" : "ThemeThumbnailVariants.axaml"));
        var selectors = styles.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value ?? string.Empty)
            .ToArray();

        Assert.Contains(hostClass, markup.Root!.Attribute("Classes")?.Value.Split(' ') ?? []);
        Assert.DoesNotContain(selectors, selector => selector.Contains($"controls|{component}", StringComparison.Ordinal));
        Assert.Contains($".{hostClass} controls|AppBorder.{surfaceClass}", selectors);
    }

    [Fact]
    public void ThemePreview_UsesSharedScenesForLightDarkAndSystemModes()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var preview = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemePreview.axaml"));
        var sceneMarkup = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemePreviewScene.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "ThemeThumbnailVariants.axaml"));
        var frame = Assert.Single(preview.Root!.Elements());
        Assert.Equal("True", frame.Attribute("ClipToBounds")?.Value);
        Assert.Equal(3, preview.Descendants().Count(element => element.Name.LocalName == "AppThemePreviewScene"));

        foreach (var scene in new[] { "preview-standard-scene", "preview-system-scene" })
        {
            Assert.Contains(preview.Descendants(), element =>
                element.Attribute("Classes")?.Value == scene);
        }

        foreach (var part in new[] { "preview-screen", "preview-sidebar", "preview-message-line",
            "preview-accent-line", "preview-composer", "preview-send" })
        {
            Assert.Contains(sceneMarkup.Descendants(), element => element.Name.LocalName == "AppBorder"
                && element.Attribute("Classes")?.Value.Split(' ').Contains(part) == true);
        }

        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            .Contains(".app-theme-preview.preview-dark .app-theme-preview-scene controls|AppBorder.preview-screen", StringComparison.Ordinal) == true);
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == ".app-theme-preview.selected controls|AppBorder.preview-selection-outline");
        Assert.Null(preview.Root?.Attribute("Width"));
        Assert.Null(preview.Root?.Attribute("Height"));
    }

    [Theory]
    [InlineData("Light", "#202020")]
    [InlineData("Dark", "#EEEEEE")]
    public void SelectedThemeOutline_ContrastsWithTheActiveTheme(string theme, string color)
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var app = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var dictionary = Assert.Single(app.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == theme));
        var brush = Assert.Single(dictionary.Elements(), element => element.Attributes()
            .Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "ThemeChoiceSelectionBrush"));
        Assert.Equal(color, brush.Attribute("Color")?.Value);

        var picker = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemeModePicker.axaml"));
        var choice = Assert.Single(picker.Descendants(), element =>
            element.Name.LocalName == "AppButton" && element.Attribute("CommandParameter")?.Value == theme);
        Assert.Equal($"{{Binding Is{theme}ThemeSelected}}", choice.Attributes()
            .Single(attribute => attribute.Name.LocalName == "Classes.selected").Value);
        Assert.Equal("{Binding SelectThemeCommand}", choice.Attribute("Command")?.Value);
        Assert.Contains(choice.Descendants(), element => element.Name.LocalName == "AppThemePreview"
            && element.Attribute("Classes")?.Value == $"preview-{theme.ToLowerInvariant()}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
