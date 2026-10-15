using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationPaneInputLifecycleTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OutsideClickDoesNotReachPrimaryAndKeyboardStaysInDrawer(bool dark) => _session.Dispatch(() =>
    {
        var invoked = 0;
        var primary = new AppButton { Content = "Primary", Command = new RelayCommand(() => invoked++) };
        var first = new AppButton { Content = "First" };
        var last = new AppButton { Content = "Last" };
        var navigation = new StackPanel { Children = { first, last } };
        var pane = new AppNavigationPanes { PrimaryPane = primary, NavigationPane = navigation };
        var window = new AppWindow { Width = 470, Height = 500, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Flush(window);
            Assert.True(primary.Focus());
            pane.ToggleNavigationCommand.Execute(null);
            Flush(window);
            Assert.Same(first, window.FocusManager!.GetFocusedElement());
            for (var index = 0; index < 6; index++)
            {
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Assert.Contains(window.FocusManager.GetFocusedElement(), new[] { first, last });
            }
            primary.Focus();
            Flush(window);
            Assert.Same(first, window.FocusManager.GetFocusedElement());
            window.MouseDown(new Point(420, 250), MouseButton.Right);
            window.MouseUp(new Point(420, 250), MouseButton.Right);
            Assert.True(pane.IsOverlayOpen);
            window.MouseDown(new Point(420, 250), MouseButton.Left);
            window.MouseUp(new Point(420, 250), MouseButton.Left);
            Flush(window);
            Assert.False(pane.IsOverlayOpen);
            Assert.Equal(0, invoked);
            Assert.Same(primary, window.FocusManager.GetFocusedElement());
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EscapeFromWindowToolbarClosesDrawerAndRestoresPrimaryFocus() => _session.Dispatch(() =>
    {
        var draft = new AppInput { Text = "Draft" };
        var pane = new AppNavigationPanes { PrimaryPane = draft, NavigationPane = new AppButton { Content = "Navigation" } };
        var toolbar = new AppButton { Content = "Toggle", Command = pane.ToggleNavigationCommand };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { toolbar, pane } };
        Grid.SetRow(pane, 1);
        var window = new AppWindow { Width = 470, Height = 500, Content = layout };
        window.Show();
        try
        {
            Flush(window);
            Assert.True(draft.Focus());
            Assert.True(toolbar.Focus());
            pane.ToggleNavigationCommand.Execute(null);
            Flush(window);
            Assert.True(toolbar.Focus());
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Flush(window);
            Assert.False(pane.IsOverlayOpen);
            Assert.Same(draft, window.FocusManager!.GetFocusedElement());
            Assert.Equal("Draft", draft.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ChangingBreakpointRetainsDockedPreferenceWithoutRecreatingPanes(bool open) => _session.Dispatch(() =>
    {
        var primary = new AppInput { Text = "Draft" };
        var pane = new AppNavigationPanes { PrimaryPane = primary, NavigationPane = new AppButton(), IsNavigationOpen = open };
        var window = new AppWindow { Width = 700, Height = 500, Content = pane };
        window.Show();
        try
        {
            Flush(window);
            Assert.False(pane.IsCompact);
            pane.MinimumPrimaryWidth = 500;
            Flush(window);
            Assert.True(pane.IsCompact);
            pane.ToggleNavigationCommand.Execute(null);
            Assert.True(pane.IsOverlayOpen);
            pane.NavigationWidth = 180;
            Flush(window);
            Assert.False(pane.IsCompact);
            Assert.False(pane.IsOverlayOpen);
            Assert.Equal(open, pane.IsNavigationOpen);
            Assert.Equal(open, pane.IsNavigationVisible);
            Assert.Same(primary, pane.PrimaryPane);
            Assert.Equal("Draft", primary.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReplacementAndDetachInvalidatePendingFocusAndUnavailableCommands() => _session.Dispatch(() =>
    {
        var removed = new AppButton { Content = "Removed" };
        var replacement = new AppButton { Content = "Replacement" };
        var pane = new AppNavigationPanes { PrimaryPane = new AppInput(), NavigationPane = removed };
        var parent = new AppBorder { Child = pane };
        var window = new AppWindow { Width = 470, Height = 500, Content = parent };
        window.Show();
        try
        {
            Flush(window);
            pane.ToggleNavigationCommand.Execute(null);
            pane.NavigationPane = replacement;
            Flush(window);
            Assert.False(pane.IsOverlayOpen);
            Assert.NotSame(removed, window.FocusManager!.GetFocusedElement());
            pane.ToggleNavigationCommand.Execute(null);
            Flush(window);
            Assert.Same(replacement, window.FocusManager.GetFocusedElement());
            parent.IsEnabled = false;
            Assert.False(pane.IsOverlayOpen);
            Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
            parent.IsEnabled = true;
            pane.ToggleNavigationCommand.Execute(null);
            parent.Child = null;
            Flush(window);
            Assert.False(pane.IsOverlayOpen);
            Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
            parent.Child = pane;
            Flush(window);
            pane.ToggleNavigationCommand.Execute(null);
            Flush(window);
            Assert.Same(replacement, window.FocusManager.GetFocusedElement());
            pane.DataContext = new object();
            Assert.False(pane.IsOverlayOpen);
            pane.NavigationPane = null;
            Assert.False(pane.ToggleNavigationCommand.CanExecute(null));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public Task InvalidPaneMetricsAreRejected(double width) => _session.Dispatch(() =>
    {
        var pane = new AppNavigationPanes();
        Assert.Throws<ArgumentException>(() => pane.NavigationWidth = width);
        Assert.Throws<ArgumentException>(() => pane.MinimumPrimaryWidth = width);
    }, TestContext.Current.CancellationToken);

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
