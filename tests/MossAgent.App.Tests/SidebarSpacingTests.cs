using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarSpacingTests
{
    [Fact]
    public void ActiveNavigationIcon_UsesSharedFilledAppearance()
    {
        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "NavigationRailVariants.axaml"));
        var selected = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value
                == "controls|AppButton.navigation-rail-button.selected");
        Assert.Equal(["Background", "Foreground", "IsFilledIcon", "IsAlternateIcon"], selected.Elements()
            .Select(element => element.Attribute("Property")?.Value));
        Assert.Equal("controls:AppIconButton", selected.Attributes().Single(attribute =>
            attribute.Name.LocalName == "SetterTargetType").Value);
        Assert.All(selected.Elements().Where(setter => setter.Attribute("Property")?.Value
            is "IsFilledIcon" or "IsAlternateIcon"), setter => Assert.Equal("True", setter.Attribute("Value")?.Value));
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var window = XDocument.Load(Path.Combine(appPath, "Controls", "AppNavigationRail.axaml"));
        Assert.Contains(window.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("AlternateIconData")?.Value == "{DynamicResource UiIconHomeFilled}");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StreamGeometry"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "UiIconHomeFilled"));
    }

    [Fact]
    public void HeaderAndPinnedSection_UseReferenceVerticalRhythm()
    {
        var sidebar = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "WorkspaceSidebar.axaml"));
        var surface = Assert.Single(sidebar.Root!.Elements());
        var layout = Assert.Single(surface.Elements());
        var header = Assert.Single(layout.Elements(), element =>
            element.Name.LocalName == "StackPanel" && element.Attribute("Grid.Row")?.Value == "0");
        Assert.Null(header.Attribute("Margin"));
        Assert.Equal("sidebar-actions-header", header.Attribute("Classes")?.Value);
        Assert.Equal("{Binding TaskManagement.PinnedTasks.HasItems}", header.Attribute("Classes.has-pinned-tasks")?.Value);
        Assert.Equal("6,0,2,11", Assert.Single(header.Elements(), element =>
            element.Name.LocalName == "Grid").Attribute("Margin")?.Value);

        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "NavigationActionVariants.axaml"));
        var pinned = Assert.Single(layout.Descendants(), element =>
            element.Attribute("Classes")?.Value == "sidebar-pinned-section");
        AssertStyleValue(styles, "StackPanel.sidebar-actions-header", "Margin", "0,0,0,21");
        AssertStyleValue(styles, "StackPanel.sidebar-actions-header.has-pinned-tasks", "Margin", "0,0,0,17");
        AssertStyleValue(styles, "StackPanel.sidebar-pinned-section", "Margin", "0,0,0,17");
        Assert.Equal("sidebar-section-heading sidebar-pinned-heading", Assert.Single(pinned.Elements(), element =>
            element.Name.LocalName == "AppText").Attribute("Classes")?.Value);
        AssertStyleValue(styles, "controls|AppText.sidebar-pinned-heading", "Margin", "8,0,0,10");

        Assert.Single(layout.Descendants(), element =>
            element.Attribute("Classes")?.Value == "projects-section-header transparent-hit-target");
        AssertStyleValue(styles, "controls|AppBorder.projects-section-header", "Margin", "8,0,10,5");
        AssertStyleValue(styles, "controls|AppText.sidebar-recent-heading", "Margin", "8,22,0,10");
    }

    [Fact]
    public void NewConversationAction_AlignsIconAndLabelWithSidebarRows()
    {
        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var action = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value == "controls|AppButton.sidebar-action-button");
        Assert.Contains(action.Elements(), element =>
            element.Attribute("Property")?.Value == "Padding"
            && element.Attribute("Value")?.Value == "10,2");

        var label = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value
                == "controls|AppButton.sidebar-action-button controls|AppIconLabel");
        Assert.Contains(label.Elements(), element =>
            element.Attribute("Property")?.Value == "Spacing"
            && element.Attribute("Value")?.Value == "11");
    }

    private static void AssertStyleValue(XDocument styles, string selector, string property, string value)
    {
        var style = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value == selector);
        var setter = Assert.Single(style.Elements(), element => element.Attribute("Property")?.Value == property);
        Assert.Equal(value, setter.Attribute("Value")?.Value);
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
