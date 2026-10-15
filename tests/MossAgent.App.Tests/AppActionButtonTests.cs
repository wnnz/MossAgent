using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppActionButtonTests
{
    [Fact]
    public void ActionButton_ComposesSharedIconAndText()
    {
        var button = new AppActionButton { Label = "添加提供商" };
        var layout = Assert.IsType<Grid>(button.Content);
        Assert.Equal(3, layout.ColumnDefinitions.Count);
        Assert.Equal(GridLength.Star, layout.ColumnDefinitions[0].Width);
        var iconLabel = Assert.IsType<AppIconLabel>(layout.Children[0]);
        Assert.Equal(GridLength.Auto, iconLabel.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Star, iconLabel.ColumnDefinitions[1].Width);
        Assert.Contains("owned-icon-label", iconLabel.Classes);
        Assert.Collection(iconLabel.Children,
            child =>
            {
                var icon = Assert.IsType<AppIcon>(child);
                Assert.Equal(14, icon.Size);
                Assert.Equal(VerticalAlignment.Center, icon.VerticalAlignment);
                Assert.False(icon.IsHitTestVisible);
            },
            child => Assert.Equal(VerticalAlignment.Center,
                Assert.IsType<AppText>(child).VerticalAlignment));
        Assert.False(Assert.IsType<AppText>(layout.Children[1]).IsVisible);
        button.FontSize = 15;
        button.FontWeight = FontWeight.SemiBold;
        var label = Assert.IsType<AppText>(iconLabel.Children[1]);
        Assert.Equal(15, label.FontSize);
        Assert.Equal(FontWeight.SemiBold, label.FontWeight);
        Assert.Equal("添加提供商", AutomationProperties.GetName(button));
        button.Label = "刷新";
        Assert.Equal("刷新", AutomationProperties.GetName(button));
        button.BadgeText = "开发中";
        Assert.True(Assert.IsType<AppText>(layout.Children[1]).IsVisible);
        Assert.Equal("刷新，开发中", AutomationProperties.GetName(button));
    }

    [Fact]
    public void ActionButtons_UseGeometryAndNoFontGlyphPrefixes()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            foreach (var button in document.Descendants().Where(element =>
                element.Name.LocalName == "AppActionButton"))
            {
                Assert.True(button.Attribute("IconData") is not null || button.Attribute("TrailingIconData") is not null);
                Assert.NotNull(button.Attribute("Label"));
                Assert.Null(button.Attribute("Content"));
                Assert.All(button.Elements(), element => Assert.Equal("Button.Flyout", element.Name.LocalName));
            }

            Assert.DoesNotContain(document.Descendants(), element =>
                element.Name.LocalName == "AppButton"
                && element.Attribute("Content")?.Value is { Length: > 0 } content
                && content[0] is '＋' or '↻' or '←');
        }
    }

    [Fact]
    public void StaticIconAndLabelActions_UseSharedActionButton()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            var manualActions = document.Descendants()
                .Where(element => element.Name.LocalName == "AppButton")
                .Select(element => element.Elements().ToArray())
                .Where(children => children.Length == 1
                    && children[0].Name.LocalName == "StackPanel")
                .Select(children => children[0].Elements().ToArray())
                .Where(children => children.Length == 2
                    && children[0].Name.LocalName == "AppIcon"
                    && children[1].Name.LocalName == "AppText"
                    && children[1].Attribute("Text")?.Value is { } label
                    && !label.StartsWith('{'))
                .ToArray();

            Assert.True(manualActions.Length == 0,
                $"{Path.GetFileName(file)} manually lays out a static icon-and-label action");
        }
    }

    [Fact]
    public void SidebarNewConversation_UsesSharedIconLabelLayout()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views",
            "WorkspaceSidebar.axaml");
        var document = XDocument.Load(path);
        var actions = document.Descendants()
            .Where(element => element.Name.LocalName == "AppActionButton"
                && element.Attribute("Classes")?.Value.Split(' ').Contains("sidebar-action-button") == true)
            .ToArray();

        var action = Assert.Single(actions);
        Assert.Equal("新对话", action.Attribute("Label")?.Value);
        Assert.Equal("{StaticResource UiIconCompose}", action.Attribute("IconData")?.Value);
        Assert.Equal("{Binding NewTaskCommand}", action.Attribute("Command")?.Value);
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
