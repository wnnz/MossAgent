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
public sealed class SettingsNavigationWorkspaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsHome_ReturnsWithoutCreatingATaskOrSendingTheDraft(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var home = RailButton(window, "返回工作区");
        Assert.DoesNotContain("selected", home.Classes);
        Assert.False(home.IsFilledIcon);
        Assert.Same(fixture.Main.CloseSettingsCommand, home.Command);
        Activate(home);
        window.UpdateLayout();
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.Contains("selected", RailButton(window, "新对话").Classes);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsAutomations_LeavesSettingsAndPreservesTheConversation(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        var automation = RailButton(window, "定时任务");
        Assert.DoesNotContain("selected", automation.Classes);
        Activate(automation);
        window.UpdateLayout();
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        Assert.Contains("selected", RailButton(window, "定时任务").Classes);
        fixture.Main.OpenSettingsCommand.Execute(null);
        window.UpdateLayout();
        Assert.DoesNotContain("selected", RailButton(window, "定时任务").Classes);
        Activate(RailButton(window, "返回工作区"));
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SettingsAudit_OpensTheVisibleInspector(bool dark) => WithWindow(dark, (window, fixture) =>
    {
        Activate(RailButton(window, "审计与执行记录"));
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.False(fixture.Main.Pages.IsAutomationsOpen);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.True(fixture.Main.IsInspectorAudit);
    });

    [Theory]
    [InlineData(null)]
    [InlineData("unknown")]
    [InlineData("")]
    public Task InvalidInspectorSelection_DoesNotLeaveSettings(string? tab) => WithWindow(false, (window, fixture) =>
    {
        var previous = fixture.Main.ActiveInspectorTab;
        var wasOpen = fixture.Main.IsRightPanelOpen;
        Assert.False(fixture.Main.SelectInspectorTabCommand.CanExecute(tab));
        fixture.Main.SelectInspectorTabCommand.Execute(tab);
        Assert.True(fixture.Main.IsSettingsOpen);
        Assert.Equal(previous, fixture.Main.ActiveInspectorTab);
        Assert.Equal(wasOpen, fixture.Main.IsRightPanelOpen);
    });

    [Fact]
    public Task SettingsMoreMenu_TogglesTheVisibleInspectorAndClosesTheHiddenFlyout() => WithWindow(false, (window, fixture) =>
    {
        var more = RailButton(window, "更多操作");
        var wasOpen = fixture.Main.IsRightPanelOpen;
        Activate(more);
        window.UpdateLayout();
        Assert.True(more.Flyout!.IsOpen);
        var rail = more.GetVisualAncestors().OfType<AppNavigationRail>().First();
        Activate(rail.FindControl<AppButton>("InspectorMenuButton")!);
        Dispatcher.UIThread.RunJobs();
        Assert.False(fixture.Main.IsSettingsOpen);
        Assert.Equal(!wasOpen, fixture.Main.IsRightPanelOpen);
        Assert.False(more.Flyout.IsOpen);
    });

    private Task WithWindow(bool dark, Action<MainWindow, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject;
        var task = fixture.Workspace.Catalog.SelectedTask;
        var model = fixture.Workspace.SelectedModel;
        var draft = fixture.Workspace.ComposerText;
        fixture.Main.SelectSettingsTabCommand.Execute("5");
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            WindowState = Avalonia.Controls.WindowState.Normal,
            Width = 1280,
            Height = 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            action(window, fixture);
            Assert.Same(project, fixture.Workspace.Catalog.SelectedProject);
            Assert.Same(task, fixture.Workspace.Catalog.SelectedTask);
            Assert.Same(model, fixture.Workspace.SelectedModel);
            Assert.Equal(draft, fixture.Workspace.ComposerText);
            Assert.False(fixture.Workspace.IsRunning);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppIconButton RailButton(MainWindow window, string label) =>
        Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), button =>
            button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == label);

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus(NavigationMethod.Tab));
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
