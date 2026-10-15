using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppearanceDiffPreviewTests
{
    [Fact]
    public void AppearancePageUsesReadOnlySharedDiffPreviewWithRealScrollers()
    {
        var appDirectory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var appearance = XDocument.Load(Path.Combine(appDirectory, "Views", "AppearanceSettingsPane.axaml"));
        var preview = XDocument.Load(Path.Combine(appDirectory, "Controls", "AppDiffPreview.axaml"));

        Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppDiffPreview");
        Assert.Null(preview.Root?.Attribute("IsHitTestVisible"));
        Assert.Equal(2, preview.Descendants().Count(element => element.Name.LocalName == "AppScrollViewer"
            && element.Attribute("HorizontalScrollBarVisibility")?.Value == "Visible"
            && element.Attribute("VerticalScrollBarVisibility")?.Value == "Disabled"));
        Assert.Equal(3, preview.Descendants().Count(element =>
            element.Attribute("Classes")?.Value == "diff-preview-removed"));
        Assert.Equal(3, preview.Descendants().Count(element =>
            element.Attribute("Classes")?.Value == "diff-preview-added"));
        Assert.DoesNotContain(preview.Descendants(), element =>
            element.Name.LocalName is "Button" or "AppButton");
        Assert.All(preview.Descendants().Where(element => element.Name.LocalName == "AppText"),
            element => Assert.Contains("code-font", element.Attribute("Classes")?.Value));
        Assert.Equal(10, preview.Descendants().Count(element => element.Name.LocalName == "Grid"
            && element.Attribute("ColumnDefinitions")?.Value == "23,33,*"));
    }

    [Fact]
    public void AppearancePage_UsesSharedCompactChoicesAndRetainsThemeEditors()
    {
        var appDirectory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var appearance = XDocument.Load(Path.Combine(appDirectory, "Views", "AppearanceSettingsPane.axaml"));
        var variants = XDocument.Load(Path.Combine(appDirectory, "Styles", "ThemeModePickerVariants.axaml"));
        var picker = XDocument.Load(Path.Combine(appDirectory, "Controls", "AppThemeModePicker.axaml"));
        var card = XDocument.Load(Path.Combine(appDirectory, "Controls", "AppThemePaletteCard.axaml"));
        var diff = Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppDiffPreview");

        var advanced = Assert.Single(diff.Ancestors(), element => element.Name.LocalName == "AppExpander");
        Assert.Null(advanced.Attribute("IsExpanded"));
        Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppThemePaletteCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette}");
        Assert.Single(advanced.Descendants(), element => element.Name.LocalName == "AppThemeExtendedFontsCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette.Fonts}");
        Assert.DoesNotContain(card.Descendants(), element => element.Name.LocalName == "AppThemeExtendedFontsCard");
        Assert.Single(card.Descendants(), element => element.Name.LocalName == "AppPillComboBox"
            && element.Attribute("SelectedItem")?.Value.StartsWith("{Binding Fonts.") == true);
        Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppThemeModePicker");
        Assert.Equal(3, picker.Descendants().Count(element => element.Name.LocalName == "AppButton"));
        Assert.Contains(variants.Descendants(), element => element.Attribute("Selector")?.Value
            == ".app-theme-mode-picker controls|AppButton.theme-mode-button"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "Height"
                && setter.Attribute("Value")?.Value == "60"));
        foreach (var (mode, selected) in new[]
        {
            ("System", "IsSystemThemeSelected"),
            ("Light", "IsLightThemeSelected"),
            ("Dark", "IsDarkThemeSelected")
        })
        {
            var button = Assert.Single(picker.Descendants(), element => element.Name.LocalName == "AppButton"
                && element.Attribute("CommandParameter")?.Value == mode);
            var preview = Assert.Single(button.Descendants(), element => element.Name.LocalName == "AppThemePreview");
            Assert.Equal($"{{Binding {selected}}}", button.Attribute("Classes.selected")?.Value);
            Assert.Contains(preview.Ancestors(), element => element.Name.LocalName == "AppViewbox"
                && element.Attribute("Classes")?.Value == "theme-mode-thumbnail");
        }
    }

    [Fact]
    public void DiffPreviewColorsAndBorderUseSharedStyles()
    {
        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "ThemePreviewVariants.axaml"));
        var selectors = styles.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var variant in new[]
        {
            "diff-preview-surface", "diff-preview-divider", "diff-preview-removed",
            "diff-preview-added", "diff-preview-remove-rule", "diff-preview-add-rule"
        })
        {
            Assert.Contains($".app-diff-preview controls|AppBorder.{variant}", selectors);
        }
        var typography = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == ".app-diff-preview controls|AppText.type-10");
        Assert.Contains(typography.Elements(), element => element.Attribute("Property")?.Value == "FontSize"
            && element.Attribute("Value")?.Value == "12");
    }

    [Fact]
    public void DiffPreviewSyntaxTokensUseSharedThemeColors()
    {
        var appDirectory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var preview = XDocument.Load(Path.Combine(appDirectory, "Controls", "AppDiffPreview.axaml"));
        var styles = XDocument.Load(Path.Combine(appDirectory, "Styles", "ThemePreviewVariants.axaml"));
        (string Variant, string Resource)[] tokens =
        [
            ("diff-token-keyword", "DiffPreviewKeywordBrush"),
            ("diff-token-identifier", "DiffPreviewIdentifierBrush"),
            ("diff-token-type", "DiffPreviewTypeBrush"),
            ("diff-token-property", "DiffPreviewPropertyBrush"),
            ("diff-token-string", "DiffPreviewStringBrush"),
            ("diff-token-number", "DiffPreviewNumberBrush")
        ];

        foreach (var (variant, resource) in tokens)
        {
            Assert.Contains(preview.Descendants(), element => element.Name.LocalName == "AppText"
                && element.Attribute("Classes")?.Value.Split(' ').Contains(variant) == true);
            var style = Assert.Single(styles.Descendants(), element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == $".app-diff-preview controls|AppText.{variant}");
            Assert.Contains(style.Elements(), element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "Foreground"
                && element.Attribute("Value")?.Value == $"{{DynamicResource {resource}}}");
            foreach (var theme in styles.Descendants().Where(element =>
                element.Name.LocalName == "ResourceDictionary"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                    && attribute.Value is "Light" or "Dark")))
            {
                Assert.Contains(theme.Elements(), element => element.Name.LocalName == "SolidColorBrush"
                    && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                        && attribute.Value == resource));
            }
        }
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
