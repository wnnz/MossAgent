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
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsTitlebarNavigationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WideSettingsKeepReferenceMenuInsetWithoutHiddenButtonInput(bool dark) => WithWindow(dark, false, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var navigation = window.FindControl<SettingsOverlay>("SettingsView")!.Navigation;
        var toggle = Toggle(window);
        Assert.False(navigation.IsCompact);
        Assert.True(navigation.IsNavigationVisible);
        Assert.False(toggle.IsVisible);
        Assert.False(toggle.Focus());
        var menus = Menus(window);
        Assert.Equal(new Thickness(5, 0, 0, 0), menus.Margin);
        foreach (var menu in menus.Children.OfType<AppButton>())
        {
            Assert.Equal(14, menu.FontSize);
            Assert.Equal(new Thickness(6, 3), menu.Padding);
            Assert.Equal(new Size(40, 30), menu.Bounds.Size);
        }
        var file = menus.GetVisualDescendants().OfType<AppButton>().First(button => Equals(button.Content, "文件"));
        Assert.Equal(77, file.TranslatePoint(default, window)!.Value.X);
        Click(window, file);
        Assert.True(Assert.IsType<AppFlyout>(file.Flyout).IsOpen);
        Assert.True(navigation.IsNavigationVisible);
        Assert.False(navigation.IsOverlayOpen);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Flush(window);
        Assert.False(file.Flyout.IsOpen);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task HiddenWideNavigationCanStillBeReopenedFromTitlebar(bool dark) => WithWindow(dark, false, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var navigation = window.FindControl<SettingsOverlay>("SettingsView")!.Navigation;
        navigation.IsNavigationOpen = false;
        Flush(window);
        Assert.False(navigation.IsNavigationVisible);
        Assert.True(Toggle(window).IsVisible);
        Assert.Equal(new Thickness(10, 0, 0, 0), Menus(window).Margin);
        Click(window, Toggle(window));
        Assert.True(navigation.IsNavigationOpen);
        Assert.True(navigation.IsNavigationVisible);
        Assert.False(Toggle(window).IsVisible);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CompactDrawerRetainsItsToggleForPointerKeyboardAndResize(bool dark) => WithWindow(dark, true, (window, fixture) =>
    {
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var navigation = window.FindControl<SettingsOverlay>("SettingsView")!.Navigation;
        var toggle = Toggle(window);
        Assert.True(navigation.IsCompact);
        Assert.True(toggle.IsVisible);
        Assert.Equal(new Thickness(10, 0, 0, 0), Menus(window).Margin);
        Click(window, toggle);
        Assert.True(navigation.IsOverlayOpen);
        Assert.True(toggle.IsVisible);
        Click(window, toggle);
        Assert.False(navigation.IsOverlayOpen);
        var restoredFocus = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());
        Assert.True(AppPageSwitcher.IsAvailableFocusTarget(restoredFocus));
        Assert.True(toggle.Focus(NavigationMethod.Tab));
        toggle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        toggle.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
        Flush(window);
        Assert.True(navigation.IsOverlayOpen);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Flush(window);
        Assert.False(navigation.IsOverlayOpen);
        Assert.Same(restoredFocus, window.FocusManager!.GetFocusedElement());
        window.Width = 1280;
        Flush(window);
        Assert.False(navigation.IsCompact);
        Assert.True(navigation.IsNavigationVisible);
        Assert.False(toggle.IsVisible);
        window.Width = 526;
        Flush(window);
        Assert.True(toggle.IsVisible);
        Assert.False(navigation.IsOverlayOpen);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task WorkspaceKeepsItsIndependentNavigationAndMenuGeometry(bool dark) => WithWindow(dark, false, (window, fixture) =>
    {
        var workspace = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        workspace.IsNavigationOpen = false;
        Flush(window);
        var toggle = Toggle(window);
        Assert.True(toggle.IsVisible);
        Assert.Equal(72, toggle.TranslatePoint(default, window)!.Value.X);
        Assert.Equal(new Thickness(10, 0, 0, 0), Menus(window).Margin);
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!.Navigation;
        Assert.False(toggle.IsVisible);
        Assert.True(settings.IsNavigationOpen);
        fixture.Main.CloseSettingsCommand.Execute(null);
        Flush(window);
        Assert.True(toggle.IsVisible);
        Assert.False(workspace.IsNavigationOpen);
        Click(window, toggle);
        Assert.True(workspace.IsNavigationOpen);
        Assert.True(settings.IsNavigationOpen);
        Assert.Equal(new Thickness(10, 0, 0, 0), Menus(window).Margin);
        Assert.Empty(fixture.Server.Requests);
    });

    private Task WithWindow(bool dark, bool compact, Action<MainWindow, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = compact ? 526 : 1280,
            Height = compact ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { Flush(window); action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppIconButton Toggle(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
        button => button.Classes.Contains("titlebar-panel"));

    private static StackPanel Menus(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<StackPanel>(),
        panel => panel.Classes.Contains("titlebar-menu-group"));

    private static void Click(MainWindow window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Flush(window);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
