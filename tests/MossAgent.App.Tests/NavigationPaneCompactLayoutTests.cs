using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationPaneCompactLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 470, true)]
    [InlineData(true, 470, true)]
    [InlineData(false, 623, true)]
    [InlineData(true, 623, true)]
    [InlineData(false, 624, false)]
    [InlineData(true, 624, false)]
    public Task CompactLayoutRetainsPrimaryAndDockedPreference(bool dark, double width, bool compact) => _session.Dispatch(() =>
    {
        var draft = new AppInput { Text = "保留未发送内容" };
        var navigation = new AppButton { Content = "选择" };
        var pane = new AppNavigationPanes { PrimaryPane = draft, NavigationPane = navigation };
        var window = new AppWindow { Width = width, Height = 500, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.Equal(compact, pane.IsCompact);
            Assert.Equal(!compact, pane.IsNavigationVisible);
            Assert.True(pane.IsNavigationOpen);
            Assert.Equal(width - (compact ? 0 : 264), draft.Bounds.Width);
            pane.ToggleNavigationCommand.Execute(null);
            window.UpdateLayout();
            Assert.Equal(compact, pane.IsOverlayOpen);
            Assert.Equal(compact, pane.IsNavigationOpen);
            Assert.Equal(compact, pane.IsNavigationVisible);
            window.Width = 900;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(pane.IsCompact);
            Assert.False(pane.IsOverlayOpen);
            Assert.Equal(compact, pane.IsNavigationVisible);
            Assert.Same(draft, pane.PrimaryPane);
            Assert.Same(navigation, pane.NavigationPane);
            Assert.Equal("保留未发送内容", draft.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DrawerBlocksPrimaryAndRestoresFocusAfterEscapeOrSelection(bool dark) => _session.Dispatch(() =>
    {
        var draft = new AppInput { Text = "不能丢失草稿" };
        var selection = new AppButton { Content = "选择" };
        AppNavigationPanes.SetDismissOnClick(selection, true);
        var pane = new AppNavigationPanes { PrimaryPane = draft, NavigationPane = selection };
        var window = new AppWindow { Width = 470, Height = 500, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(draft.Focus());
            foreach (var select in new[] { false, true })
            {
                pane.ToggleNavigationCommand.Execute(null);
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.True(pane.IsOverlayOpen);
                var primary = Assert.Single(pane.GetVisualDescendants().OfType<AppContentControl>(), presenter => ReferenceEquals(presenter.Content, draft));
                Assert.False(primary.IsHitTestVisible);
                Assert.True(draft.IsEffectivelyEnabled);
                Assert.Same(selection, window.FocusManager!.GetFocusedElement());
                if (select) selection.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.UpdateLayout();
                Assert.False(pane.IsOverlayOpen);
                Assert.True(primary.IsHitTestVisible);
                Assert.Same(draft, window.FocusManager.GetFocusedElement());
                Assert.Equal("不能丢失草稿", draft.Text);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosedOrUnavailableHostRejectsStaleCommands() => _session.Dispatch(() =>
    {
        var pane = new AppNavigationPanes { PrimaryPane = new AppInput(), NavigationPane = new AppButton() };
        Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
        pane.ToggleNavigationCommand.Execute(null);
        Assert.False(pane.IsOverlayOpen);
        var parent = new AppBorder { Child = pane };
        var window = new AppWindow { Width = 470, Height = 500, Content = parent };
        window.Show();
        try
        {
            window.UpdateLayout();
            pane.ToggleNavigationCommand.Execute(null);
            Assert.True(pane.IsOverlayOpen);
            parent.IsVisible = false;
            Assert.False(pane.IsOverlayOpen);
            Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
            pane.ToggleNavigationCommand.Execute(null);
            Assert.False(pane.IsOverlayOpen);
            parent.IsVisible = true;
            pane.IsEnabled = false;
            Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
            pane.ToggleNavigationCommand.Execute(null);
            Assert.False(pane.IsOverlayOpen);
        }
        finally { window.Close(); }
        Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
        pane.ToggleNavigationCommand.Execute(null);
        Assert.False(pane.IsOverlayOpen);
    }, TestContext.Current.CancellationToken);
}
