using Avalonia;
using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceInspectorOwnershipTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("browser")]
    [InlineData("terminal")]
    [InlineData("diff")]
    [InlineData("audit")]
    [InlineData("sources")]
    public Task SelectingAnotherTask_DoesNotInheritThePreviousInspector(string tab) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture, "first");
        fixture.Main.SelectInspectorTabCommand.Execute(tab);
        var second = await AddTaskAsync(fixture, "second");
        AssertDefaults(fixture.Main);
        fixture.Main.SelectInspectorTabCommand.Execute("audit");
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(first);
        Assert.Equal(tab, fixture.Main.ActiveInspectorTab);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.False(fixture.Main.IsContextCardVisible);
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(second);
        Assert.Equal("audit", fixture.Main.ActiveInspectorTab);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosedInspectorAndDismissedContextCard_AreRestoredForTheirTask() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var first = await AddTaskAsync(fixture, "first");
        fixture.Main.SelectInspectorTabCommand.Execute("terminal");
        fixture.Main.ToggleContextCardCommand.Execute(null);
        fixture.Main.ToggleContextCardCommand.Execute(null);
        await AddTaskAsync(fixture, "second");
        AssertDefaults(fixture.Main);
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(first);
        Assert.Equal("terminal", fixture.Main.ActiveInspectorTab);
        Assert.False(fixture.Main.IsRightPanelOpen);
        Assert.False(fixture.Main.IsContextCardVisible);
        fixture.Main.ToggleRightPanelCommand.Execute(null);
        Assert.True(fixture.Main.IsInspectorTerminal);
        fixture.Main.ToggleRightPanelCommand.Execute(null);
        Assert.False(fixture.Main.IsContextCardVisible);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task NewConversation_KeepsItsOwnInspectorInsteadOfTheSelectedTasks() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.ToggleContextCardCommand.Execute(null);
        var first = await AddTaskAsync(fixture, "first");
        AssertDefaults(fixture.Main);
        fixture.Main.SelectInspectorTabCommand.Execute("diff");
        fixture.Workspace.Catalog.NewTaskCommand.Execute(null);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.Equal("home", fixture.Main.ActiveInspectorTab);
        Assert.False(fixture.Main.IsRightPanelOpen);
        Assert.False(fixture.Main.IsContextCardVisible);
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(first);
        Assert.True(fixture.Main.IsInspectorDiff);
        Assert.True(fixture.Main.IsRightPanelOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProjectDrafts_DoNotShareInspectorPresentation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var catalog = fixture.Workspace.Catalog;
        var first = catalog.SelectedProject!;
        var second = first with { Id = Guid.NewGuid(), Name = "second" };
        await fixture.Tasks.SaveProjectAsync(second, fixture.Token);
        catalog.Projects.Add(second);
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        catalog.SelectProjectCommand.Execute(second);
        AssertDefaults(fixture.Main);
        catalog.SelectProjectCommand.Execute(first);
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("title")]
    [InlineData("status")]
    [InlineData("background")]
    public Task UpdatingTaskRecords_DoesNotResetOrStealTheInspector(string change) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var task = await AddTaskAsync(fixture, "selected");
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        if (change == "background")
            fixture.Workspace.Catalog.UpsertTask(task with { Id = Guid.NewGuid(), Title = "scheduled background" }, select: false);
        else fixture.Workspace.Catalog.UpsertTask(change == "title" ? task with { Title = "new title" }
            : task with { Status = AgentTaskStatus.Running });
        Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask!.Id);
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
        Assert.False(fixture.Main.IsContextCardVisible);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RestoredContextCardInset_DoesNotOverlapMessages() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SetContextCardHeight(196);
        fixture.Main.SetChatViewportWidth(1000);
        var first = await AddTaskAsync(fixture, "first");
        fixture.Main.SelectInspectorTabCommand.Execute("diff");
        await AddTaskAsync(fixture, "second");
        Assert.Equal(new Thickness(0, 218, 0, 0), fixture.Main.ChatMessageViewportMargin);
        fixture.Workspace.Catalog.SelectTaskCommand.Execute(first);
        Assert.Equal(default, fixture.Main.ChatMessageViewportMargin);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposedWindow_DoesNotFollowLaterTaskSelections() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await AddTaskAsync(fixture, "first");
        fixture.Main.SelectInspectorTabCommand.Execute("browser");
        fixture.Main.Dispose();
        await AddTaskAsync(fixture, "second");
        Assert.True(fixture.Main.IsInspectorBrowser);
        Assert.True(fixture.Main.IsRightPanelOpen);
    }, TestContext.Current.CancellationToken);

    private static void AssertDefaults(MainWindowViewModel model)
    {
        Assert.Equal("home", model.ActiveInspectorTab);
        Assert.False(model.IsRightPanelOpen);
        Assert.True(model.IsContextCardVisible);
    }

    private static async Task<AgentTask> AddTaskAsync(WorkspaceModelSelectionFixture fixture, string title)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), fixture.Workspace.Catalog.SelectedProject!.Id, title,
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        fixture.Workspace.Catalog.UpsertTask(task);
        return task;
    }
}
