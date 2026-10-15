using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class DraftConversationHeaderTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, false, false)]
    [InlineData(1280, false, false)]
    [InlineData(1920, false, false)]
    [InlineData(980, true, false)]
    [InlineData(1280, true, false)]
    [InlineData(1920, true, false)]
    [InlineData(980, false, true)]
    [InlineData(1280, false, true)]
    [InlineData(1920, false, true)]
    [InlineData(980, true, true)]
    [InlineData(1280, true, true)]
    [InlineData(1920, true, true)]
    public Task NewConversation_HidesOldHeaderAndHistoryNavigationRestoresIt(
        double width, bool dark, bool multiple) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var catalog = fixture.Workspace.Catalog;
        var tabs = fixture.Workspace.Tabs;
        var first = await AddTaskAsync(fixture, "First synthetic conversation");
        var second = await AddTaskAsync(fixture, "Second synthetic conversation");
        await catalog.ReloadAsync();
        catalog.SelectTaskCommand.Execute(first);
        if (multiple) catalog.SelectTaskCommand.Execute(second);
        var oldTabs = tabs.Items.ToArray();
        var selected = tabs.SelectedTab;
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = width,
            Height = width < 1000 ? 640 : 820,
            WindowState = WindowState.Normal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var view = window.GetVisualDescendants().OfType<WorkspaceConversationTabsView>().Single();
            var heading = view.FindControl<AppIconLabel>("ConversationHeading")!;
            var strip = view.GetVisualDescendants().OfType<AppSelectionScrollViewer>().Single();
            var newTask = window.GetVisualDescendants().OfType<AppActionButton>().Single(button =>
                button.Classes.Contains("sidebar-action-button") && ReferenceEquals(button.Command, catalog.NewTaskCommand));
            ConversationTabControlTests.Press(newTask);
            window.UpdateLayout();
            Assert.Null(catalog.SelectedTask);
            Assert.Null(tabs.SelectedTab);
            Assert.False(heading.IsEffectivelyVisible);
            Assert.False(strip.IsEffectivelyVisible);
            Assert.All(tabs.Items, tab => Assert.False(tab.IsSelected));
            Assert.Equal(oldTabs, tabs.Items);
            Assert.False(tabs.CloseTabCommand.CanExecute(null));
            catalog.NavigateBackCommand.Execute(null);
            window.UpdateLayout();
            Assert.Same(selected, tabs.SelectedTab);
            Assert.Equal(!multiple, heading.IsEffectivelyVisible);
            Assert.Equal(multiple, strip.IsEffectivelyVisible);
            catalog.NavigateForwardCommand.Execute(null);
            window.UpdateLayout();
            Assert.Null(tabs.SelectedTab);
            Assert.False(heading.IsEffectivelyVisible);
            Assert.False(strip.IsEffectivelyVisible);
            Assert.Equal(2, (await fixture.Tasks.GetTasksAsync(first.ProjectId, fixture.Token)).Count);
            Assert.Empty(await fixture.Tasks.GetArchivedTasksAsync(first.ProjectId, fixture.Token));
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LockedNewConversation_PreservesTheActiveHeaderAndDisposalClearsIt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var catalog = fixture.Workspace.Catalog;
        var tabs = fixture.Workspace.Tabs;
        Assert.Null(tabs.SelectedTab);
        Assert.False(tabs.IsSingleTab);
        Assert.False(tabs.ShowTabStrip);
        var task = await AddTaskAsync(fixture, "Locked synthetic conversation");
        await catalog.ReloadAsync();
        catalog.SelectTaskCommand.Execute(task);
        var selected = tabs.SelectedTab;
        catalog.IsLocked = true;
        catalog.NewTaskCommand.Execute(null);
        Assert.Equal(task.Id, catalog.SelectedTask?.Id);
        Assert.Same(selected, tabs.SelectedTab);
        Assert.True(tabs.IsSingleTab);
        Assert.False(tabs.ShowTabStrip);
        catalog.IsLocked = false;
        catalog.NewTaskCommand.Execute(null);
        Assert.Null(tabs.SelectedTab);
        Assert.False(tabs.ShowTabStrip);
        tabs.Dispose();
        catalog.SelectTaskCommand.Execute(task);
        Assert.Empty(tabs.Items);
        Assert.Null(tabs.SelectedTab);
        Assert.False(tabs.ShowTabStrip);
        Assert.False(tabs.SelectTabCommand.CanExecute(selected));
        Assert.False(tabs.CloseTabCommand.CanExecute(selected));
        Assert.Single(await fixture.Tasks.GetTasksAsync(task.ProjectId, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture, string title)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id,
            title, ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        return task;
    }
}
