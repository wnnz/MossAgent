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
public sealed class ConversationTabBindingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task ActualWorkspaceMenu_ClosesItsOwnTabAndLeavesTheConversationPersisted(bool dark, bool background) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var catalog = fixture.Workspace.Catalog;
        var project = catalog.SelectedProject!;
        var now = DateTimeOffset.UtcNow;
        var first = new AgentTask(Guid.NewGuid(), project.Id, "First synthetic conversation", ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Completed, now, now, null);
        var second = first with { Id = Guid.NewGuid(), Title = "Second synthetic conversation" };
        await fixture.Tasks.SaveTaskAsync(first, fixture.Token);
        await fixture.Tasks.SaveTaskAsync(second, fixture.Token);
        await catalog.ReloadAsync();
        catalog.SelectTaskCommand.Execute(first);
        catalog.SelectTaskCommand.Execute(second);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = 980,
            Height = 640,
            WindowState = WindowState.Normal,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var tabs = fixture.Workspace.Tabs;
            Assert.Equal(2, tabs.Items.Count);
            var target = tabs.Items.Single(tab => tab.Task.Id == (background ? first.Id : second.Id));
            var component = window.GetVisualDescendants().OfType<AppConversationTab>().Single(tab => ReferenceEquals(tab.CommandParameter, target));
            Assert.Equal(target.Title, component.Title);
            Assert.Equal(target.IsSelected, component.IsSelected);
            var options = component.FindControl<AppIconButton>("OptionsButton")!;
            var flyout = Assert.IsType<AppFlyout>(options.Flyout);
            flyout.ShowAt(options);
            window.UpdateLayout();
            var close = Assert.IsType<AppButton>(Assert.IsType<AppCard>(flyout.Content).Child);
            Assert.Same(tabs.CloseTabCommand, close.Command);
            Assert.Same(target, close.CommandParameter);
            catalog.IsLocked = true;
            Assert.False(flyout.IsOpen);
            Assert.False(close.IsEffectivelyEnabled);
            tabs.CloseTabCommand.Execute(target);
            Assert.Equal(2, tabs.Items.Count);
            catalog.IsLocked = false;
            flyout.ShowAt(options);
            ConversationTabControlTests.Press(close);
            window.UpdateLayout();
            Assert.False(flyout.IsOpen);
            Assert.Equal(background ? second.Id : first.Id, Assert.Single(tabs.Items).Task.Id);
            Assert.Equal(tabs.SelectedTab!.Task.Id, catalog.SelectedTask!.Id);
            Assert.Equal(2, (await fixture.Tasks.GetTasksAsync(project.Id, fixture.Token)).Count);
            Assert.Empty(await fixture.Tasks.GetArchivedTasksAsync(project.Id, fixture.Token));
            Assert.True(window.GetVisualDescendants().OfType<WorkspaceConversationTabsView>().Single()
                .FindControl<AppIconLabel>("ConversationHeading")!.IsEffectivelyVisible);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WorkspaceDisposal_RetiresItsTabCommandsAndCatalogSubscriptions() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var catalog = workspace.Catalog;
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), catalog.SelectedProject!.Id, "Disposed synthetic conversation",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        await catalog.ReloadAsync();
        catalog.SelectTaskCommand.Execute(task);
        var target = Assert.Single(workspace.Tabs.Items);
        workspace.Dispose();
        workspace.Dispose();
        catalog.StartNewTask();
        catalog.UpsertTask(task with { Title = "Updated after workspace disposal" }, select: false);
        Assert.Empty(workspace.Tabs.Items);
        Assert.Null(workspace.Tabs.SelectedTab);
        Assert.False(workspace.Tabs.SelectTabCommand.CanExecute(target));
        Assert.False(workspace.Tabs.CloseTabCommand.CanExecute(target));
        workspace.Tabs.SelectTabCommand.Execute(target);
        workspace.Tabs.CloseTabCommand.Execute(target);
        Assert.Null(catalog.SelectedTask);
        Assert.Single(await fixture.Tasks.GetTasksAsync(task.ProjectId, fixture.Token));
        Assert.Empty(await fixture.Tasks.GetArchivedTasksAsync(task.ProjectId, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
