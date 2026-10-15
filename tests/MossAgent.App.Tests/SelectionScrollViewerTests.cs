using System.Xml.Linq;
using Avalonia.Controls.Primitives;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SelectionScrollViewerTests
{
    [Theory]
    [InlineData(0, 300, 1000, 480, 238, 418)]
    [InlineData(418, 300, 1000, 0, 238, 0)]
    [InlineData(100, 300, 1000, 120, 100, 100)]
    [InlineData(0, 200, 1000, 350, 238, 350)]
    [InlineData(0, 300, 500, 480, 100, 200)]
    [InlineData(100, 600, 500, 0, 238, 0)]
    [InlineData(100, 300, 1000, -10, 100, 0)]
    public void SelectionReveal_UsesMinimalBoundedScrolling(double offset, double viewport,
        double extent, double start, double width, double expected)
    {
        Assert.Equal(expected,
            AppSelectionScrollViewer.CalculateHorizontalOffset(offset, viewport, extent, start, width));
    }

    [Theory]
    [InlineData(0, 1000, 480, 238)]
    [InlineData(double.NaN, 1000, 480, 238)]
    [InlineData(300, double.PositiveInfinity, 480, 238)]
    [InlineData(300, 1000, 480, 0)]
    [InlineData(300, 1000, double.NaN, 238)]
    public void InvalidOrUnarrangedSelection_DoesNotInventAnOffset(double viewport,
        double extent, double start, double width)
    {
        Assert.Equal(100,
            AppSelectionScrollViewer.CalculateHorizontalOffset(100, viewport, extent, start, width));
    }

    [Fact]
    public void ConversationTabs_UseTheSelectedItemAndHorizontalWrapper()
    {
        var viewer = new AppSelectionScrollViewer();
        Assert.Equal(ScrollBarVisibility.Hidden, viewer.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, viewer.VerticalScrollBarVisibility);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        var markup = XDocument.Load(Path.Combine(directory!.FullName, "src", "MossAgent.App",
            "Views", "WorkspaceConversationTabsView.axaml"));
        var scroller = Assert.Single(markup.Descendants(), element => element.Name.LocalName == "AppSelectionScrollViewer");
        Assert.Equal("{Binding Tabs.SelectedTab}", scroller.Attribute("SelectedItem")?.Value);
    }
}
