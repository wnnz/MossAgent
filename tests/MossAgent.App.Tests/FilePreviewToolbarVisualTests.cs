using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class FilePreviewToolbarVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 300)]
    [InlineData(true, 300)]
    [InlineData(false, 640)]
    [InlineData(true, 640)]
    public Task ToolbarActionsAreAlignedBoundedAndLive(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var window = new AppWindow
        {
            Content = preview,
            Width = width,
            Height = 450,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var layout = preview.FindControl<Grid>("FileToolbarLayout")!;
            Assert.Equal(4, layout.ColumnDefinitions.Count);
            var actions = layout.GetVisualDescendants().OfType<AppButton>().Where(button => button.IsEffectivelyVisible).ToArray();
            Assert.Equal(width < 400, preview.IsToolbarCompact);
            Assert.Equal(width < 400 ? 4 : 6, actions.Length);
            var centers = actions.Select(action => action.TranslatePoint(new Point(0, action.Bounds.Height / 2), window)!.Value.Y).ToArray();
            Assert.InRange(centers.Max() - centers.Min(), 0, 0.5);
            Assert.All(actions, action =>
            {
                Assert.Equal(action.Classes.Contains("file-action-outline") ? 34 : 32, action.Bounds.Height);
                Assert.True(action.TranslatePoint(new Point(0, 0), window)!.Value.X >= 0);
                Assert.True(action.TranslatePoint(new Point(action.Bounds.Width, 0), window)!.Value.X <= width);
                Assert.NotNull(Assert.Single(action.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsEffectivelyVisible).Data);
                Assert.Equal(action.Classes.Contains("file-toolbar-chevron") ? 10 : action.Classes.Contains("file-toolbar-copy") ? 12 : 14,
                    Assert.Single(action.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsEffectivelyVisible).Size);
            });
            Assert.All(layout.Children.OfType<AppBorder>().Where(border => border.IsEffectivelyVisible), border =>
            {
                Assert.Equal(34, border.Bounds.Height);
                Assert.Equal((Avalonia.Media.BoxShadows)window.FindResource("SharedSurfaceShadow")!, border.BoxShadow);
            });
            var copy = preview.FindControl<AppIconButton>("CopyDefaultButton")!;
            Assert.True(copy.IsEffectivelyEnabled);
            await preview.CopyActions.CopyDefaultCommand.ExecuteAsync(null);
            Assert.Equal(fixture.Pane.Active!.Text, await window.Clipboard!.TryGetTextAsync());
            Assert.Single(fixture.Calls);
            var reveal = preview.FindControl<AppActionButton>("RevealButton")!;
            Assert.Same(fixture.Pane.RevealCommand, reveal.Command);
            var revealIcon = Assert.Single(reveal.GetVisualDescendants().OfType<AppIcon>(), icon => !icon.Classes.Contains("action-trailing-icon"));
            Assert.Same(window.FindResource("FilePreviewExplorerDrawing"), revealIcon.Drawing);
            Assert.Null(Assert.Single(reveal.GetVisualDescendants().OfType<AppIcon>(), icon => icon.Classes.Contains("action-trailing-icon")).Drawing);
            Assert.Null(Assert.Single(copy.GetVisualDescendants().OfType<AppIcon>()).Drawing);
            await fixture.Pane.RevealCommand.ExecuteAsync(null);
            Assert.Single(fixture.Calls, call => call.Request.ToolName == "filesystem.reveal_file");
            copy.Focus();
            window.KeyPress(Key.C, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.C, "c");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            await preview.CopyActions.CopyPathCommand.ExecutionTask!;
            Assert.Equal(fixture.Pane.Active.Path, await window.Clipboard!.TryGetTextAsync());
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("tab")]
    [InlineData("hide")]
    [InlineData("detach")]
    [InlineData("self-hidden")]
    [InlineData("ancestor-hidden")]
    [InlineData("self-disabled")]
    [InlineData("ancestor-disabled")]
    public Task MenusBindActualCommandsAndCloseOnOwnershipChanges(string change) => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture();
        await fixture.OpenAsync();
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var host = new AppBorder { Child = preview };
        var window = new AppWindow { Content = host, Width = 640, Height = 450 };
        window.Show();
        try
        {
            window.UpdateLayout();
            var copyMenu = preview.FindControl<AppIconButton>("CopyMenuButton")!;
            var flyout = (AppFlyout)copyMenu.Flyout!;
            flyout.ShowAt(copyMenu);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(flyout.IsOpen);
            var card = (AppCard)flyout.Content!;
            var buttons = card.GetVisualDescendants().OfType<AppButton>().ToArray();
            Assert.Equal(3, buttons.Length);
            Assert.All(buttons, button =>
            {
                Assert.Equal(new Thickness(0), button.BorderThickness);
                Assert.Equal(Avalonia.Layout.HorizontalAlignment.Left, button.HorizontalContentAlignment);
            });
            Assert.Same(preview.CopyActions.CopyPathCommand, buttons[0].Command);
            Assert.Same(preview.CopyActions.CopyRelativePathCommand, buttons[1].Command);
            Assert.Same(preview.CopyActions.CopyContentCommand, buttons[2].Command);
            flyout.Hide();
            var openMenu = preview.FindControl<AppIconButton>("OpenMenuButton")!;
            var openFlyout = (AppFlyout)openMenu.Flyout!;
            openFlyout.ShowAt(openMenu);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Same(fixture.Pane.RevealCommand, ((AppButton)((AppCard)openFlyout.Content!).Child!).Command);
            openFlyout.Hide();
            flyout.ShowAt(copyMenu);
            if (change == "tab") await fixture.OpenAsync("new.cs");
            else if (change == "hide") fixture.Pane.Hide();
            else if (change == "self-hidden") preview.IsVisible = false;
            else if (change == "ancestor-hidden") host.IsVisible = false;
            else if (change == "self-disabled") preview.IsEnabled = false;
            else if (change == "ancestor-disabled") host.IsEnabled = false;
            else window.Content = null;
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.False(flyout.IsOpen);
            if (change != "tab") Assert.False(preview.CopyActions.CanShowMenu);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReadOnlyImageCopiesPathAndDisablesTextActionsAndExternalOpen() => _session.DispatchAsync(async () =>
    {
        using var fixture = new FilePreviewActionFixture(MossAgent.Domain.ApprovalPolicy.ReadOnly);
        await fixture.OpenAsync("synthetic.png");
        fixture.Pane.Active!.Image = new Avalonia.Media.Imaging.WriteableBitmap(new PixelSize(10, 10), new Vector(96, 96),
            Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        var preview = new AppFilePreview { DataContext = fixture.Pane };
        var window = new AppWindow { Content = preview, Width = 300, Height = 450 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.False(preview.FindControl<AppActionButton>("RevealButton")!.IsEffectivelyEnabled);
            Assert.False(preview.FindControl<AppIconButton>("OpenMenuButton")!.IsEffectivelyEnabled);
            var imageAction = preview.FindControl<AppIconButton>("ImageViewButton")!;
            var imageIcon = Assert.Single(imageAction.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(12, imageIcon.Size);
            Assert.Equal(1, imageIcon.StrokeThickness);
            Assert.Equal(34, imageAction.Bounds.Width);
            Assert.Equal(34, imageAction.Bounds.Height);
            Assert.False(preview.CopyActions.CopyContentCommand.CanExecute(null));
            Assert.False(fixture.Pane.Active.Search.ShowCommand.CanExecute(null));
            await preview.CopyActions.CopyDefaultCommand.ExecuteAsync(null);
            Assert.Equal(fixture.Pane.Active.Path, await window.Clipboard!.TryGetTextAsync());
            Assert.Single(fixture.Calls);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
