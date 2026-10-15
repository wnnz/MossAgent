using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class NavigationRailPointerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task TooltipsStayOutsideEveryRailButton(bool dark, bool compact) => WithWindow(dark, compact, (window, fixture) =>
    {
        foreach (var button in window.GetVisualDescendants().OfType<AppIconButton>()
            .Where(button => button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button")).ToArray())
        {
            Assert.Equal(PlacementMode.Right, ToolTip.GetPlacement(button));
            var tooltip = new ToolTip { Content = button.Label };
            ToolTip.SetTip(button, tooltip);
            try
            {
                window.MouseMove(Center(button, window));
                ToolTip.SetIsOpen(button, true);
                Flush(window);
                var buttonBounds = new Rect(button.PointToScreen(default).ToPoint(1), button.Bounds.Size);
                var tooltipBounds = new Rect(tooltip.PointToScreen(default).ToPoint(1), tooltip.Bounds.Size);
                Assert.True(tooltipBounds.Width > 0 && tooltipBounds.Height > 0);
                Assert.True(tooltipBounds.Left >= buttonBounds.Right + 8, $"{button.Label}: {tooltipBounds}; {buttonBounds}");
                Assert.False(buttonBounds.Intersects(tooltipBounds));
            }
            finally { ToolTip.SetIsOpen(button, false); }
        }
        Assert.Empty(fixture.Server.Requests);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task CenterClicksOpenSettingsAndReturnWithoutChangingTheConversation(bool dark, bool compact) => WithWindow(dark, compact, (window, fixture) =>
    {
        var task = fixture.Workspace.Catalog.SelectedTask;
        var project = fixture.Workspace.Catalog.SelectedProject;
        fixture.Workspace.ComposerText = "unsent draft";
        var navigation = window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
        var wasOpen = navigation.IsNavigationOpen;
        Click(window, Button(window, "设置"));
        Assert.True(fixture.Main.IsSettingsOpen);
        Click(window, Button(window, "返回工作区"));
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.Equal(wasOpen, navigation.IsNavigationOpen);
        Assert.Same(task, fixture.Workspace.Catalog.SelectedTask);
        Assert.Same(project, fixture.Workspace.Catalog.SelectedProject);
        Assert.Equal("unsent draft", fixture.Workspace.ComposerText);
        Assert.False(fixture.Workspace.IsRunning);
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

    private static AppIconButton Button(MainWindow window, string label) => Assert.Single(window.GetVisualDescendants()
        .OfType<AppIconButton>(), button => button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == label);

    private static Point Center(Control control, MainWindow window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static void Click(MainWindow window, AppIconButton button)
    {
        var position = Center(button, window);
        window.MouseMove(position);
        ToolTip.SetIsOpen(button, true);
        Flush(window);
        window.MouseDown(position, MouseButton.Left);
        window.MouseUp(position, MouseButton.Left);
        Flush(window);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }
}
