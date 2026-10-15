using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewCompactToolbarTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ResizePreservesReadableFilenameAndRestoresWideActions(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync("PreviewSample.cs");
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var window = new AppWindow
        {
            Content = preview,
            Width = 640,
            Height = 450,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            foreach (var width in new[] { 640, 300, 399, 400, 640 })
            {
                window.Width = width;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(width < 400, preview.IsToolbarCompact);
                Assert.Equal(width < 400, preview.FindControl<AppIconButton>("MoreActionsButton")!.IsEffectivelyVisible);
                Assert.Equal(width >= 400, preview.FindControl<AppActionButton>("RevealButton")!.IsEffectivelyVisible);
                var layout = preview.FindControl<Grid>("FileToolbarLayout")!;
                Assert.Equal(4, layout.ColumnDefinitions.Count);
                var rightEdge = layout.GetVisualDescendants().OfType<AppButton>().Where(action => action.IsEffectivelyVisible)
                    .Max(action => action.TranslatePoint(new Point(action.Bounds.Width, 0), layout)!.Value.X);
                Assert.InRange(layout.Bounds.Width - rightEdge, 0, 1);
                var breadcrumb = Assert.Single(layout.GetVisualDescendants().OfType<AppFileBreadcrumb>());
                Assert.InRange(breadcrumb.Bounds.Width, 100, width);
                var name = Assert.Single(breadcrumb.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("file-breadcrumb-name"));
                Assert.Equal("PreviewSample.cs", name.Text);
                Assert.True(name.DesiredSize.Width > 0);
                Assert.Equal(Math.Min(name.DesiredSize.Width, breadcrumb.Bounds.Width), name.Bounds.Width, 3);
                var shadow = (BoxShadows)window.FindResource("SharedSurfaceShadow")!;
                foreach (var action in layout.GetVisualDescendants().OfType<AppButton>()
                    .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("file-action-outline")))
                {
                    var presenter = Assert.Single(action.GetVisualDescendants().OfType<ContentPresenter>(), child => child.Name == "PART_ContentPresenter");
                    Assert.Equal(shadow, presenter.BoxShadow);
                }
            }
            Assert.Single(fixture.Calls);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(ApprovalPolicy.ReadOnly)]
    [InlineData(ApprovalPolicy.FullAccess)]
    public Task CompactMenuUsesExistingAuthorizedCommands(ApprovalPolicy policy) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture(policy);
        await fixture.OpenAsync();
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var window = new AppWindow { Content = preview, Width = 300, Height = 450 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = preview.FindControl<AppIconButton>("MoreActionsButton")!;
            var menu = (AppFlyout)button.Flyout!;
            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            var actions = ((AppCard)menu.Content!).GetVisualDescendants().OfType<AppActionButton>().ToArray();
            Assert.Equal(2, actions.Length);
            Assert.Same(fixture.Pane.RevealCommand, actions[0].Command);
            Assert.Same(fixture.Pane.RefreshCommand, actions[1].Command);
            Assert.Equal(policy != ApprovalPolicy.ReadOnly, actions[0].IsEffectivelyEnabled);
            Assert.True(actions[1].IsEffectivelyEnabled);
            Assert.All(actions, action => Assert.Equal(new Thickness(0), action.BorderThickness));
            if (policy != ApprovalPolicy.ReadOnly)
            {
                await Assert.IsAssignableFrom<IAsyncRelayCommand>(actions[0].Command).ExecuteAsync(null);
                menu.ShowAt(button);
                Dispatcher.UIThread.RunJobs();
                Assert.True(menu.IsOpen);
            }
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(actions[1].Command).ExecuteAsync(null);
            Assert.Equal(2, fixture.Calls.Count(call => call.Request.ToolName == "filesystem.preview_file"));
            Assert.Equal(policy == ApprovalPolicy.ReadOnly ? 0 : 1,
                fixture.Calls.Count(call => call.Request.ToolName == "filesystem.reveal_file"));
            Assert.All(fixture.Calls, call => Assert.Equal(fixture.Context.TaskId, call.Context.TaskId));
            Assert.True(fixture.Pane.Active!.IsReady);
            Assert.False(menu.IsOpen);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("resize")]
    [InlineData("tab")]
    [InlineData("context")]
    [InlineData("policy")]
    [InlineData("hide")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("detached")]
    public Task CompactMenuClosesWhenItsOwnerIsInvalidated(string change) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var host = new AppBorder { Child = preview };
        var window = new AppWindow { Content = host, Width = 300, Height = 450 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var button = preview.FindControl<AppIconButton>("MoreActionsButton")!;
            var menu = (AppFlyout)button.Flyout!;
            menu.ShowAt(button);
            Dispatcher.UIThread.RunJobs();
            Assert.True(menu.IsOpen);
            switch (change)
            {
                case "resize": window.Width = 640; window.UpdateLayout(); break;
                case "tab": await fixture.OpenAsync("second.cs"); break;
                case "context": preview.DataContext = null; break;
                case "policy": fixture.Pane.Attach(fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly }); break;
                case "hide": fixture.Pane.Hide(); break;
                case "hidden": host.IsVisible = false; break;
                case "disabled": host.IsEnabled = false; break;
                case "detached": host.Child = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            Dispatcher.UIThread.RunJobs();
            Assert.False(menu.IsOpen);
            Assert.Equal(change == "tab" ? 2 : 1, fixture.Calls.Count);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
