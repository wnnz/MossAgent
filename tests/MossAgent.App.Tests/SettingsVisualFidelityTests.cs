using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SettingsVisualFidelityTests
{
    [Fact]
    public void SettingsShell_UsesMeasuredTypographyColorAndSpacing()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var typography = XDocument.Load(Path.Combine(appPath, "Styles", "TypographyVariants.axaml"));
        var surfaces = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var layout = XDocument.Load(Path.Combine(appPath, "Styles", "SettingsLayoutVariants.axaml"));
        var appearance = XDocument.Load(Path.Combine(appPath, "Views", "AppearanceSettingsPane.axaml"));
        var navigation = XDocument.Load(Path.Combine(appPath, "Views", "SettingsOverlay.axaml"));

        AssertSetter(typography, "controls|AppText.settings-title", "FontSize", "28");
        AssertSetter(typography, "controls|AppText.settings-title", "FontWeight", "Normal");
        AssertSetter(layout, "controls|AppText.settings-sidebar-title", "FontSize", "18");
        AssertSetter(layout, "controls|AppText.settings-sidebar-title", "LineHeight", "28");
        AssertSetter(layout, ".app-input.settings-search", "Height", "36");
        AssertSetter(surfaces, "controls|AppBorder.settings-sidebar-surface", "Background",
            "{DynamicResource WorkspaceSidebarBackgroundBrush}");
        AssertSetter(layout, "Grid.settings-content-column", "Margin", "28,74,38,34");
        AssertSetter(layout, "StackPanel.titlebar-menu-group", "Margin", "10,0,0,0");
        var appearanceNavigation = Assert.Single(navigation.Descendants(), element =>
            element.Name.LocalName == "AppSettingsNavButton" && LocalizableText.Unwrap(element.Attribute("Label")?.Value) == "外观");
        Assert.Equal("{StaticResource UiIconAppearance}", appearanceNavigation.Attribute("IconData")?.Value);

        var section = Assert.Single(appearance.Descendants(), element =>
            element.Name.LocalName == "AppText"
            && element.Attribute("Text")?.Value == "{x:Static resources:AppearanceText.VisualStyle}");
        Assert.Contains("settings-theme-heading", section.Attribute("Classes")?.Value);
        Assert.Null(section.Attribute("Margin"));
        AssertSetter(layout, "controls|AppText.settings-theme-heading", "Margin", "0,41,0,16");
        AssertSetter(typography, "controls|AppText.settings-theme-heading", "FontSize", "14");
        AssertSetter(typography, "controls|AppText.settings-theme-heading", "LineHeight", "20");
    }

    private static void AssertSetter(XDocument document, string selector, string property, string value)
    {
        var style = Assert.Single(document.Descendants(), element =>
            element.Attribute("Selector")?.Value == selector);
        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == property
            && element.Attribute("Value")?.Value == value);
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
