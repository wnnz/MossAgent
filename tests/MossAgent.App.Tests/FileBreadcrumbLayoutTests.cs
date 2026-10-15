using Avalonia;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FileBreadcrumbLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 30)]
    [InlineData(true, 30)]
    [InlineData(false, 180)]
    [InlineData(true, 180)]
    [InlineData(false, 360)]
    [InlineData(true, 360)]
    [InlineData(false, 754)]
    [InlineData(true, 754)]
    public Task FilenameHasPriorityAndResizingNeverOverflows(bool dark, int width) => _session.Dispatch(() =>
    {
        var breadcrumb = new AppFileBreadcrumb
        {
            ParentPath = "project > 很长的文件夹路径 > component-samples",
            FileName = "combo-focus-comparison.png"
        };
        var window = new AppWindow
        {
            Content = breadcrumb,
            Width = width,
            Height = 80,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var name = Assert.Single(breadcrumb.Children.OfType<AppText>(), text => text.Classes.Contains("file-breadcrumb-name"));
            var parent = Assert.Single(breadcrumb.Children.OfType<AppText>(), text => text.Classes.Contains("file-breadcrumb-parent"));
            Assert.Equal(breadcrumb.FileName, name.Text);
            Assert.Equal(FontWeight.SemiBold, name.FontWeight);
            Assert.Equal(TextTrimming.CharacterEllipsis, name.TextTrimming);
            Assert.InRange(name.Bounds.Right, 0, breadcrumb.Bounds.Width + 0.5);
            Assert.InRange(name.Bounds.X, 0, breadcrumb.Bounds.Width);
            if (width <= 180) Assert.False(parent.IsVisible);
            else Assert.True(parent.IsVisible);
            window.Width = 30;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(parent.IsVisible);
            Assert.Equal(0, name.Bounds.X);
            Assert.InRange(name.Bounds.Width, 0, 30);
            window.Width = 754;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(parent.IsVisible, $"window={window.Bounds}; breadcrumb={breadcrumb.Bounds}; name={name.Bounds}; desired={breadcrumb.DesiredSize}");
            Assert.Equal(name.DesiredSize.Width, name.Bounds.Width, 3);
            Assert.True(name.Bounds.Width > 30);
            Assert.InRange(name.Bounds.Right, 0, breadcrumb.Bounds.Width + 0.5);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
