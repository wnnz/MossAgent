using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedWindowAppearanceTests
{
    [Fact]
    public void WindowAndResizeGrips_UseSharedVariants()
    {
        var appPath = AppPath();
        var window = XDocument.Load(Path.Combine(appPath, "Views", "MainWindow.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "WindowVariants.axaml"));
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var background = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value == ".app-window");

        Assert.Null(window.Root!.Attribute("Background"));
        Assert.Equal("{DynamicResource AppBackgroundBrush}", Assert.Single(background.Elements())
            .Attribute("Value")?.Value);
        Assert.Contains(application.Descendants(), element => element.Attribute("Source")?.Value
            == "avares://MossAgent.App/Styles/WindowVariants.axaml");

        var grips = window.Descendants().Where(element => element.Attribute("PointerPressed")?.Value
            == "HandleResizeGripPointerPressed").ToArray();
        Assert.Equal(8, grips.Length);
        Assert.All(grips, grip =>
        {
            Assert.Null(grip.Attribute("Cursor"));
            Assert.Null(grip.Attribute("Width"));
            Assert.Null(grip.Attribute("Height"));
            Assert.Contains("transparent-hit-target", grip.Attribute("Classes")?.Value ?? string.Empty);
        });
        foreach (var (direction, variant) in new[]
        {
            ("North", "resize-north-south"), ("South", "resize-north-south"),
            ("West", "resize-west-east"), ("East", "resize-west-east"),
            ("NorthWest", "resize-north-west"), ("NorthEast", "resize-north-east"),
            ("SouthWest", "resize-south-west"), ("SouthEast", "resize-south-east")
        })
            Assert.Contains(variant, Assert.Single(grips, grip => grip.Attribute("Tag")?.Value == direction)
                .Attribute("Classes")?.Value ?? string.Empty);
    }

    [Fact]
    public void WorkspaceFrame_UsesSharedAppearanceWithoutChangingNativeWindowHitAreas()
    {
        var window = XDocument.Load(Path.Combine(AppPath(), "Views", "MainWindow.axaml"));
        var frame = Assert.Single(window.Descendants(), element => element.Attribute("Classes")?.Value
            == "window-content-frame");
        Assert.Equal("AppBorder", frame.Name.LocalName);
        Assert.Equal("1", frame.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", frame.Attribute("Grid.ColumnSpan")?.Value);
        Assert.Null(frame.Attribute("CornerRadius"));
        Assert.Null(frame.Attribute("Margin"));
        Assert.Null(frame.Attribute("BoxShadow"));
        Assert.DoesNotContain(frame.Descendants(), element => element.Attribute("PointerPressed")?.Value
            == "HandleResizeGripPointerPressed");
    }

    [Theory]
    [InlineData("resize-north-south", "Height", "6")]
    [InlineData("resize-north-south", "Cursor", "SizeNorthSouth")]
    [InlineData("resize-west-east", "Width", "6")]
    [InlineData("resize-west-east", "Cursor", "SizeWestEast")]
    [InlineData("resize-corner", "Width", "10")]
    [InlineData("resize-corner", "Height", "10")]
    [InlineData("resize-north-west", "Cursor", "TopLeftCorner")]
    [InlineData("resize-north-east", "Cursor", "TopRightCorner")]
    [InlineData("resize-south-west", "Cursor", "BottomLeftCorner")]
    [InlineData("resize-south-east", "Cursor", "BottomRightCorner")]
    public void ResizeVariants_PreserveNativeInteractionGeometry(string variant, string property, string value)
    {
        var styles = XDocument.Load(Path.Combine(AppPath(), "Styles", "WindowVariants.axaml"));
        var style = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppBorder." + variant);

        Assert.Contains(style.Elements(), setter => setter.Attribute("Property")?.Value == property
            && setter.Attribute("Value")?.Value == value);
    }

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
