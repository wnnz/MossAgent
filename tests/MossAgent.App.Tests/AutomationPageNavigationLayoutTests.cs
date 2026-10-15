using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationPageNavigationLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, true, 1280, 820)]
    [InlineData(true, true, 1280, 820)]
    [InlineData(false, false, 1280, 820)]
    [InlineData(true, false, 1280, 820)]
    [InlineData(false, true, 1920, 1032)]
    [InlineData(true, true, 1920, 1032)]
    [InlineData(false, false, 1920, 1032)]
    [InlineData(true, false, 1920, 1032)]
    [InlineData(false, true, 526, 601)]
    [InlineData(true, true, 526, 601)]
    [InlineData(false, false, 526, 601)]
    [InlineData(true, false, 526, 601)]
    public Task AutomationPageOmitsConversationNavigationAndRestoresItsPreference(
        bool dark, bool sidebarOpen, int width, int height) => WithWindow(dark, width, height, (window, fixture) =>
    {
        fixture.Main.IsLeftPanelOpen = sidebarOpen;
        Flush(window);
        var navigation = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        var project = fixture.Workspace.Catalog.SelectedProject;
        var draft = fixture.Workspace.ComposerText;
        var inspectorOpen = fixture.Main.IsRightPanelOpen;
        Activate(RailButton(window, "定时任务"));
        Flush(window);

        AssertAutomationLayout(window);
        Assert.Equal(sidebarOpen, fixture.Main.IsLeftPanelOpen);
        Assert.Equal(inspectorOpen, fixture.Main.IsRightPanelOpen);
        Assert.Same(project, fixture.Workspace.Catalog.SelectedProject);
        Assert.Equal(draft, fixture.Workspace.ComposerText);

        fixture.Main.SelectSettingsTabCommand.Execute("5");
        Flush(window);
        var settings = window.FindControl<SettingsOverlay>("SettingsView")!;
        Assert.True(settings.IsEffectivelyVisible);
        Assert.True(settings.Navigation.IsCompact || settings.Navigation.IsNavigationVisible);
        if (settings.Navigation.IsCompact)
        {
            Assert.True(Toggle(window).IsEffectivelyVisible);
            Activate(Toggle(window));
            Flush(window);
            Assert.True(settings.Navigation.IsOverlayOpen);
            Activate(Toggle(window));
            Flush(window);
            Assert.False(settings.Navigation.IsOverlayOpen);
        }
        else Assert.False(Toggle(window).IsEffectivelyVisible);
        fixture.Main.CloseSettingsCommand.Execute(null);
        Flush(window);
        AssertAutomationLayout(window);

        Activate(RailButton(window, "新对话"));
        Flush(window);
        Assert.False(fixture.Main.Pages.IsAutomationsOpen);
        Assert.True(navigation.IsEffectivelyVisible);
        Assert.Equal(sidebarOpen, navigation.IsNavigationOpen);
        Assert.Equal(!navigation.IsCompact && sidebarOpen, navigation.IsNavigationVisible);
        Assert.True(Toggle(window).IsEffectivelyVisible);
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OpeningAutomationsClosesCompactDrawerWithoutChangingWidePreference(bool dark) =>
        WithWindow(dark, 526, 601, (window, fixture) =>
    {
        var navigation = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        Activate(Toggle(window));
        Flush(window);
        Assert.True(navigation.IsOverlayOpen);
        fixture.Main.OpenAutomationsCommand.Execute(null);
        Flush(window);
        Assert.False(navigation.IsOverlayOpen);
        AssertAutomationLayout(window);
        window.Width = 1280;
        Flush(window);
        AssertAutomationLayout(window);
        Activate(RailButton(window, "新对话"));
        Flush(window);
        Assert.True(navigation.IsNavigationVisible);
        Assert.True(fixture.Main.IsLeftPanelOpen);
        Assert.Empty(fixture.Server.Requests);
    });

    private static void AssertAutomationLayout(MainWindow window)
    {
        var navigation = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        var automation = Assert.Single(window.GetVisualDescendants().OfType<AutomationsView>());
        Assert.True(automation.IsEffectivelyVisible);
        Assert.False(navigation.IsEffectivelyVisible);
        Assert.False(navigation.ToggleNavigationCommand.CanExecute(null));
        Assert.DoesNotContain(automation.GetVisualAncestors(), ancestor => ancestor is AppNavigationPanes);
        var frame = window.FindControl<AppBorder>("WorkspaceFrame")!;
        Assert.Equal(frame.TranslatePoint(default, window)!.Value.X, automation.TranslatePoint(default, window)!.Value.X);
        Assert.Equal(frame.Bounds.Width, automation.Bounds.Width);
        Assert.False(Toggle(window).IsEffectivelyVisible);
        Assert.False(Toggle(window).Focus());
        var tint = Assert.Single(window.GetVisualDescendants().OfType<AppBorder>(), border =>
            border.Classes.Contains("sidebar-surface") && border.GetVisualAncestors().OfType<AppBorder>()
                .Any(ancestor => ancestor.Classes.Contains("titlebar-surface")));
        Assert.False(tint.IsEffectivelyVisible);
        Assert.True(RailButton(window, "新对话").IsEffectivelyVisible);
        var focused = Assert.IsAssignableFrom<Control>(window.FocusManager!.GetFocusedElement());
        Assert.False(navigation.IsVisualAncestorOf(focused));
    }

    private Task WithWindow(bool dark, int width, int height, Action<MainWindow, WorkspaceModelSelectionFixture> action) =>
        _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = width,
            Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { Flush(window); action(window, fixture); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppIconButton Toggle(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
        button => button.Classes.Contains("titlebar-panel"));

    private static AppIconButton RailButton(MainWindow window, string label) => Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(),
        button => button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == label);

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
