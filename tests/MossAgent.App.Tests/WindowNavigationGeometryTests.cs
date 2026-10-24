using System.Xml.Linq;
using MossAgent.App.Resources;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WindowNavigationGeometryTests
{
    [Fact]
    public void MainWindow_UsesReferenceShellGeometry()
    {
        var root = RepositoryRoot();
        var window = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "MainWindow.axaml"));
        var shell = window.Descendants().First(element => element.Name.LocalName == "Grid");
        var sidebar = window.Descendants().Single(element => element.Name.LocalName == "WorkspaceSidebar");
        var sidebarHeader = window.Descendants().First(element => element.Name.LocalName == "AppBorder"
            && element.Attribute("Width")?.Value == "316");
        var rail = window.Descendants().Single(element => element.Name.LocalName == "AppNavigationRail"
            && element.Attribute("Grid.Column")?.Value == "0");
        var navigation = Assert.Single(sidebar.Ancestors(), element => element.Name.LocalName == "AppNavigationPanes");
        Assert.Equal("Auto,*", shell.Attribute("RowDefinitions")?.Value);
        Assert.Equal("526", window.Root!.Attribute("MinWidth")?.Value);
        Assert.Equal("601", window.Root.Attribute("MinHeight")?.Value);
        Assert.Null(sidebar.Attribute("Width"));
        Assert.Null(sidebar.Attribute("IsVisible"));
        Assert.Equal("AppNavigationPanes.NavigationPane", sidebar.Parent?.Name.LocalName);
        Assert.Equal("{Binding IsLeftPanelOpen, Mode=TwoWay}", navigation.Attribute("IsNavigationOpen")?.Value);
        Assert.Equal("52", rail.Parent?.Elements().First(element =>
            element.Name.LocalName == "Grid.ColumnDefinitions").Elements().First().Attribute("Width")?.Value);
        Assert.Equal("316", sidebarHeader.Attribute("Width")?.Value);
        Assert.Contains(sidebarHeader.Descendants(), element => element.Name.LocalName == "Binding"
            && element.Attribute("Path")?.Value == "#WorkspaceNavigation.IsNavigationVisible");
        var frame = Assert.Single(sidebar.Ancestors(), element => element.Attribute("Classes")?.Value
            == "window-content-frame");
        Assert.Equal("1", frame.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", frame.Attribute("Grid.ColumnSpan")?.Value);
        Assert.Equal("{Binding ToggleSettingsCommand}", rail.Attribute("SettingsCommand")?.Value);
        var titleBar = shell.Elements().First(element => element.Name.LocalName == "AppBorder");
        Assert.Null(titleBar.Attribute("Padding"));
        var workspaceTitleBackground = Assert.Single(titleBar.Descendants(), element =>
            element.Name.LocalName == "Grid" && element.Elements().Any(child =>
                child.Name.LocalName == "AppBorder" && child.Attribute("Width")?.Value == "316"));
        Assert.Contains(sidebarHeader, workspaceTitleBackground.Descendants());
        var toggle = Assert.Single(titleBar.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Click")?.Value == "HandleNavigationToggle");
        Assert.Null(toggle.Attribute("IsVisible"));
        var visibility = Assert.Single(toggle.Elements(), element => element.Name.LocalName == "AppIconButton.IsVisible");
        Assert.Contains(visibility.Descendants(), element => element.Name.LocalName == "Binding"
            && element.Attribute("Path")?.Value == "#SettingsView.Navigation.IsCompact");
        var titlebarActions = Assert.Single(titleBar.Descendants(), element =>
            element.Name.LocalName == "StackPanel" && element.Attribute("Spacing")?.Value == "2");
        var menus = Assert.Single(titlebarActions.Elements(), element => element.Name.LocalName == "StackPanel");
        Assert.Equal("13", menus.Attribute("Spacing")?.Value);
        Assert.Equal("titlebar-menu-group", menus.Attribute("Classes")?.Value);
        Assert.Equal("{Binding IsSettingsOpen}", menus.Attribute("Classes.settings-mode")?.Value);
        Assert.Equal("{Binding !Navigation.IsCompact, ElementName=SettingsView}", menus.Attribute("Classes.settings-navigation-wide")?.Value);
        Assert.Equal("{Binding #SettingsView.Navigation.IsNavigationVisible}", menus.Attribute("Classes.settings-navigation-visible")?.Value);
        Assert.Equal(["文件", "编辑", "视图", "帮助"], menus.Elements()
            .Select(element => element.Name.LocalName == "AppEditMenu"
                ? WindowMenuText.Get("Edit") : LocalizableText.Unwrap(element.Attribute("Content")?.Value)));
        var variants = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Styles", "SurfaceVariants.axaml"));
        var titlebarStyle = Assert.Single(variants.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder.titlebar-surface");
        foreach (var (property, value) in new[] { ("Height", "44"), ("Padding", "4,0,0,8"), ("BorderThickness", "0") })
            Assert.Contains(titlebarStyle.Elements(), setter => setter.Attribute("Property")?.Value == property
                && setter.Attribute("Value")?.Value == value);
        var sidebarMarkup = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        var sidebarSurface = sidebarMarkup.Root!.Elements().Single(element => element.Name.LocalName == "AppBorder");
        Assert.Equal("8,8,8,12", sidebarSurface.Attribute("Padding")?.Value);
        Assert.Equal("workspace-sidebar-surface", sidebarSurface.Attribute("Classes")?.Value);
        Assert.Contains(sidebarSurface.Descendants(), element => element.Name.LocalName == "StackPanel"
            && element.Attribute("Grid.Row")?.Value == "0"
            && element.Attribute("Classes")?.Value == "sidebar-actions-header"
            && element.Attribute("Classes.has-pinned-tasks")?.Value == "{Binding TaskManagement.PinnedTasks.HasItems}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
