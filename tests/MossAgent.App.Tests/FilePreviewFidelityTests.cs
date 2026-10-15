using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 300)]
    [InlineData(true, 300)]
    [InlineData(false, 754)]
    [InlineData(true, 754)]
    public Task ImagePreview_HasCompactTabsAndNoPermanentZoomStripOrEmptyNotice(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture(MossAgent.Domain.ApprovalPolicy.ReadOnly);
        await fixture.OpenAsync("PreviewSample.cs");
        await fixture.OpenAsync("combo-focus-comparison.png");
        fixture.Pane.Active!.Image = new WriteableBitmap(new PixelSize(100, 20), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var window = new AppWindow
        {
            Width = width,
            Height = 450,
            Content = preview,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var tabs = preview.GetVisualDescendants().OfType<AppActionButton>().Where(button => button.Classes.Contains("file-tab")).ToArray();
            Assert.Equal(2, tabs.Length);
            Assert.All(tabs, tab => Assert.Equal(30, tab.Bounds.Height));
            var selected = Assert.Single(tabs, tab => tab.Classes.Contains("selected"));
            Assert.NotSame(tabs[0].IconData, selected.IconData);
            var tabHosts = preview.GetVisualDescendants().OfType<AppBorder>().Where(border => border.Classes.Contains("file-tab-host")).ToArray();
            Assert.All(tabHosts, host => Assert.Equal(32, host.Bounds.Height));
            var inactiveClose = Assert.Single(tabHosts[0].GetVisualDescendants().OfType<AppIconButton>());
            Assert.Equal(0, inactiveClose.Opacity);
            Assert.False(inactiveClose.IsHitTestVisible);
            Assert.Equal(1, Assert.Single(tabHosts[1].GetVisualDescendants().OfType<AppIconButton>()).Opacity);
            foreach (var close in tabHosts.SelectMany(host => host.GetVisualDescendants().OfType<AppIconButton>()))
            {
                Assert.Equal(new Size(24, 24), close.Bounds.Size);
                var closeIcon = Assert.IsType<AppIcon>(close.Content);
                Assert.Equal(6, closeIcon.Size);
                Assert.Equal(1, closeIcon.StrokeThickness);
                Assert.Same(preview.FindResource(window.ActualThemeVariant, "TextMutedBrush"), close.Foreground);
            }
            var notices = preview.GetVisualDescendants().OfType<AppText>().Where(text => text.Classes.Contains("file-preview-notice"));
            Assert.All(notices, text => Assert.False(text.IsEffectivelyVisible));
            Assert.DoesNotContain(preview.GetVisualDescendants().OfType<AppBorder>(), border => border.Classes.Contains("file-preview-zoom"));
            var imageOptions = preview.FindControl<AppIconButton>("ImageViewButton")!;
            Assert.True(imageOptions.IsEffectivelyEnabled);
            var menu = (AppFlyout)imageOptions.Flyout!;
            menu.ShowAt(imageOptions);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            var card = (AppCard)menu.Content!;
            var zoomIn = Assert.Single(card.GetVisualDescendants().OfType<AppIconButton>(), button => button.Command == fixture.Pane.Active.ZoomInCommand);
            zoomIn.Command!.Execute(null);
            Assert.False(fixture.Pane.Active.IsFit);
            Assert.Equal(1.25, fixture.Pane.Active.Scale);
            Assert.Equal(2, fixture.Calls.Count);
            fixture.Pane.SelectCommand.Execute(fixture.Pane.Tabs[0]);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(menu.IsOpen);
            Assert.False(imageOptions.IsEffectivelyVisible);
            Assert.True(tabs[0].Focus(NavigationMethod.Tab));
            window.UpdateLayout();
            Assert.Equal(1, inactiveClose.Opacity);
            Assert.True(inactiveClose.IsHitTestVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
