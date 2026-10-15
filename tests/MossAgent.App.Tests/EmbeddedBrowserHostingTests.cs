using System.Xml.Linq;
using Avalonia;
using MossAgent.App.Controls;
using NativeWebView.Platform.Windows;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmbeddedBrowserHostingTests
{
    [Theory]
    [InlineData(277, 820)]
    [InlineData(277, 360)]
    [InlineData(640, 600)]
    public void DirectHost_ArrangesDynamicallyAttachedNativeView(double width, double height)
    {
        using var view = new AppEmbeddedBrowserView(new WindowsNativeWebViewBackend());
        var host = new AppBorder();
        var size = new Size(width, height);
        host.Measure(size);
        host.Arrange(new Rect(size));

        host.Child = view;
        host.Measure(size);
        host.Arrange(new Rect(size));

        Assert.Equal(size, view.Bounds.Size);
        Assert.True(view.IsMeasureValid);
        Assert.True(view.IsArrangeValid);

        var resized = new Size(width - 40, height - 40);
        host.Measure(resized);
        host.Arrange(new Rect(resized));
        Assert.Equal(resized, view.Bounds.Size);

        host.Child = null;
        Assert.Null(view.Parent);
    }

    [Fact]
    public void BrowserPane_UsesSharedViewportAndWindowKeepsHostAttached()
    {
        var path = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views", "EmbeddedBrowserPane.axaml");
        var markup = XDocument.Load(path);
        var host = Assert.Single(markup.Descendants(), element => element.Attribute("View")?.Value == "{Binding Content}");

        Assert.Equal("AppBrowserViewport", host.Name.LocalName);
        Assert.Equal("{Binding Host}", host.Attribute("Host")?.Value);
        Assert.DoesNotContain(markup.Descendants(), element => element.Name.LocalName == "AppContentControl");
        Assert.Contains(markup.Descendants(), element => element.Attribute("IsVisible")?.Value == "{Binding !HasBrowser}");
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views", "MainWindow.axaml"));
        var persistent = Assert.Single(window.Root!.Elements().Single(element => element.Name.LocalName == "Grid").Elements(),
            element => element.Attribute("Child")?.Value == "{Binding EmbeddedBrowsers.Host}");
        Assert.Null(persistent.Attribute("IsVisible"));
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
