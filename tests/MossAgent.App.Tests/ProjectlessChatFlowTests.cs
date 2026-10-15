using Avalonia.Headless;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProjectlessChatFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task EmptyWorkspace_CanSendResumeAndReopenWithNoTools() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        var workspace = fixture.Workspace;
        Assert.Null(workspace.Catalog.SelectedProject);
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
        workspace.ComposerText = "无项目首条消息";
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        Assert.Null(task.ProjectId);
        Assert.Null(task.WorktreePath);
        Assert.Equal(ApprovalPolicy.ReadOnly, task.ApprovalPolicy);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
        AssertRequestHasNoTools(fixture.Server.Requests[0]);

        workspace.ComposerText = "继续聊天";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(4, (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Count);
        Assert.Contains(fixture.Server.Requests[1].GetProperty("input").EnumerateArray(),
            message => message.TryGetProperty("content", out var content) && content.ToString().Contains("无项目首条消息", StringComparison.Ordinal));
        AssertRequestHasNoTools(fixture.Server.Requests[1]);

        workspace.Catalog.NewChatCommand.Execute(null);
        Assert.Empty(workspace.Messages);
        workspace.Catalog.SelectTaskCommand.Execute(Assert.Single(workspace.Catalog.RecentTasks.Items).Task);
        await fixture.WaitUntilAsync(() => workspace.Messages.Count == 4);
        await workspace.LoadAsync();
        Assert.Equal(task.Id, workspace.Catalog.SelectedTask?.Id);
        Assert.Equal(4, workspace.Messages.Count);
        Assert.Empty(await fixture.Tasks.GetProjectsAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProjectAndChat_KeepIndependentHistoryTabsAndNavigation() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = Assert.IsType<ProjectProfile>(workspace.Catalog.SelectedProject);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var projectTask = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        workspace.Catalog.NewChatCommand.Execute(null);
        Assert.Null(workspace.Catalog.SelectedProject);
        Assert.Empty(workspace.Messages);
        workspace.ComposerText = "独立聊天";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        var chat = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        Assert.Null(chat.ProjectId);
        Assert.Equal(2, workspace.Tabs.Items.Count);
        AssertRequestHasNoTools(fixture.Server.Requests[1]);
        workspace.Catalog.SelectTaskCommand.Execute(projectTask);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == projectTask.Id && workspace.Messages.Count == 2);
        Assert.Equal(project.Id, workspace.Catalog.SelectedProject?.Id);
        workspace.Catalog.NavigateBackCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == chat.Id && workspace.Messages.Count == 2);
        Assert.Null(workspace.Catalog.SelectedProject);
        Assert.Equal("独立聊天", workspace.Messages[0].Content);
        Assert.Equal(2, workspace.Tabs.Items.Count);
        workspace.Catalog.TaskSearch.Query = "独立";
        Assert.Equal(chat.Id, Assert.Single(workspace.Catalog.RecentTasks.Items).Task.Id);
        await workspace.Catalog.TaskManagement.TogglePinCommand.ExecuteAsync(chat);
        Assert.Equal(chat.Id, Assert.Single(workspace.Catalog.TaskManagement.PinnedTasks.Items).Task.Id);
        await workspace.Catalog.TaskManagement.ArchiveCommand.ExecuteAsync(chat);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Empty(workspace.Messages);
        Assert.Single(workspace.Catalog.TaskManagement.ArchivedTasks);
        workspace.Catalog.TaskManagement.SelectedArchivedTask = workspace.Catalog.TaskManagement.ArchivedTasks[0];
        await workspace.Catalog.TaskManagement.RestoreCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => workspace.Catalog.SelectedTask?.Id == chat.Id && workspace.Messages.Count == 2);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProjectlessContextPicker_RemainsAvailableAndLabelsTheScope() => _session.Dispatch(() =>
    {
        var bar = new AppComposerContextBar
        {
            ProjectPickerContent = new AppText(),
            OpenProjectCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { })
        };
        Assert.Null(bar.Project);
        Assert.True(bar.IsProjectAvailable);
        Assert.Equal("无项目聊天", bar.ProjectLabel);
    }, TestContext.Current.CancellationToken);

    private static void AssertRequestHasNoTools(System.Text.Json.JsonElement request) =>
        Assert.True(!request.TryGetProperty("tools", out var tools) || tools.GetArrayLength() == 0);

    [Fact]
    public Task PlainChat_CanStopAndKeepsTheInterruptedUserMessage() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && workspace.HasActiveRun);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        Assert.True(workspace.PrimaryActionCommand.CanExecute(null));
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning);
        Assert.Null(task.ProjectId);
        Assert.Equal(AgentTaskStatus.Cancelled, Assert.Single(await fixture.Tasks.GetTasksAsync(null, fixture.Token)).Status);
        Assert.Equal(MessageRole.User, Assert.Single(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token)).Role);
        AssertRequestHasNoTools(fixture.Server.Requests[0]);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public async Task PlainChatContext_HasNoDirectoriesBrowserOrArtifactAccess()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var chat = await fixture.Factory.CreateAsync(null, "plain", ApprovalPolicy.FullAccess, false, fixture.Token);
        var context = fixture.Panels.CreateContext(chat, null);
        Assert.False(context.ToolsEnabled);
        Assert.Empty(context.AuthorizedRoots);
        Assert.Empty(context.WorkingDirectory);
        Assert.Null(context.BrowserSession);
        Assert.Null(context.ArtifactDirectory);
        Assert.Equal(ApprovalPolicy.ReadOnly, context.ApprovalPolicy);
        Assert.Throws<InvalidOperationException>(() => fixture.Panels.CreateContext(chat, fixture.Project));
        Assert.Throws<InvalidOperationException>(() => fixture.Panels.CreateContext(fixture.Task, null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Factory.CreateAsync(
            null, "worktree forbidden", ApprovalPolicy.FullAccess, true, fixture.Token));
    }
}
