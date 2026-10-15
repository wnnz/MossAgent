using Avalonia.Headless;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationAppFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();
    private static AutomationSchedule Schedule(WorkspaceModelSelectionFixture fixture, ModelProfile? model = null)
    {
        model ??= fixture.SelectedModel;
        return new AutomationSchedule(Guid.NewGuid(), "合成代码审查", "只读检查项目。", fixture.Workspace.Catalog.SelectedProject!.Id,
            model.ProviderId, model.Id, AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
            TimeZoneInfo.Local.Id, ApprovalPolicy.ReadOnly, 30, false, DateTimeOffset.UtcNow.AddDays(1));
    }

    [Fact]
    public Task EditorToSchedulerToConversation_IsAnActualPersistedRun() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var auto = fixture.Main.Pages.Automations!;
        var draft = fixture.Workspace.ComposerText;
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        await auto.CreateCommand.ExecuteAsync(null);
        Assert.True(auto.Editor.IsOpen);
        Assert.Null(auto.Editor.SelectedProject);
        Assert.Null(auto.Editor.SelectedModel);
        auto.Editor.Name = "合成计划";
        auto.Editor.Prompt = "只读检查项目。";
        auto.Editor.SelectedProject = Assert.Single(auto.Editor.Projects);
        auto.Editor.SelectedModel = Assert.Single(auto.Editor.Models, item => item.Model.Id == fixture.SelectedModel.Id);
        await auto.SaveCommand.ExecuteAsync(null);
        Assert.False(auto.HasError, auto.Error);
        Assert.False(auto.Editor.IsOpen);
        var item = Assert.Single(auto.Items);
        Assert.False(item.Schedule.IsEnabled);
        Assert.False(auto.RunNowCommand.CanExecute(item));
        await fixture.Scheduler.StartAsync(fixture.Token);
        await auto.ReloadAsync();
        item = Assert.Single(auto.Items);
        Assert.True(auto.RunNowCommand.CanExecute(item));
        await auto.RunNowCommand.ExecuteAsync(item);
        await fixture.WaitUntilAsync(() => auto.Runs.Any(run => run.Run.Status == AutomationRunStatus.Completed));
        Assert.Single(fixture.Server.Requests);
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        Assert.Equal(draft, fixture.Workspace.ComposerText);
        Assert.True(fixture.Main.Pages.IsAutomationsOpen);
        await auto.OpenRunCommand.ExecuteAsync(Assert.Single(auto.Runs));
        Assert.False(fixture.Main.Pages.IsAutomationsOpen);
        var task = fixture.Workspace.Catalog.SelectedTask!;
        Assert.Equal(auto.Runs[0].Run.TaskId, task.Id);
        Assert.Equal(AgentTaskStatus.Completed, task.Status);
        Assert.Contains(await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token), message => message.Role == MessageRole.Assistant);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task BackgroundRun_DoesNotConsumeDraftModelAttachmentsOrCurrentPane() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var draft = workspace.ComposerText;
        var model = workspace.SelectedModel;
        var provider = workspace.SelectedProvider;
        workspace.ApprovalPolicy = ApprovalPolicy.FullAccess;
        workspace.ModelPicker.SelectedReasoning = "高";
        fixture.Main.Diff.Show("current-pane-content");
        var reserved = Guid.NewGuid();
        var runner = new WorkspaceAutomationRunner(fixture.Tasks, fixture.Configurations, workspace);
        var result = await runner.RunAsync(Schedule(fixture, fixture.AlternativeModel), reserved, fixture.Token);
        Assert.Equal(AutomationRunStatus.Completed, result);
        Assert.Same(model, workspace.SelectedModel);
        Assert.Same(provider, workspace.SelectedProvider);
        Assert.Equal(draft, workspace.ComposerText);
        Assert.Equal(ApprovalPolicy.FullAccess, workspace.ApprovalPolicy);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Empty(workspace.Messages);
        Assert.Equal("current-pane-content", fixture.Main.Diff.Content);
        var request = Assert.Single(fixture.Server.Requests);
        Assert.Equal(fixture.AlternativeModel.ModelId, request.GetProperty("model").GetString());
        Assert.Equal("medium", request.GetProperty("reasoning").GetProperty("effort").GetString());
        var stored = Assert.Single(await fixture.Tasks.GetTasksAsync(workspace.Catalog.SelectedProject!.Id, fixture.Token));
        Assert.Equal(reserved, stored.Id);
        Assert.Equal(ApprovalPolicy.ReadOnly, stored.ApprovalPolicy);
        Assert.False(fixture.Runs.IsRunning(reserved));
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("project")]
    [InlineData("provider")]
    [InlineData("model")]
    [InlineData("startup")]
    [InlineData("shutdown")]
    public Task UnavailableSelectionAndLifecycle_DoNotFallBackOrCreateTask(string reason) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var schedule = Schedule(fixture);
        schedule = reason switch
        {
            "project" => schedule with { ProjectId = Guid.NewGuid() },
            "provider" => schedule with { ProviderId = Guid.NewGuid() },
            "model" => schedule with { ModelId = Guid.NewGuid() },
            _ => schedule
        };
        if (reason == "startup") fixture.Workspace.Startup.Begin();
        if (reason == "shutdown") await fixture.Workspace.ShutdownAsync();
        var runner = new WorkspaceAutomationRunner(fixture.Tasks, fixture.Configurations, fixture.Workspace);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(schedule, Guid.NewGuid(), fixture.Token));
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(fixture.Workspace.Catalog.SelectedProject!.Id, fixture.Token));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Cancellation_UsesTaskScopedLifecycleAndPersistsTerminalStatus() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var taskId = Guid.NewGuid();
        var runner = new WorkspaceAutomationRunner(fixture.Tasks, fixture.Configurations, fixture.Workspace);
        var execution = runner.RunAsync(Schedule(fixture), taskId, cancellation.Token);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        await cancellation.CancelAsync();
        Assert.Equal(AutomationRunStatus.Cancelled, await execution);
        Assert.False(fixture.Runs.IsRunning(taskId));
        Assert.Null(fixture.Workspace.Catalog.SelectedTask);
        var task = Assert.Single(await fixture.Tasks.GetTasksAsync(fixture.Workspace.Catalog.SelectedProject!.Id, fixture.Token));
        Assert.Equal(AgentTaskStatus.Cancelled, task.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidOrStaleEditor_StaysOpenAndNeverOverwritesStoredPlan() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var auto = fixture.Main.Pages.Automations!;
        await auto.CreateCommand.ExecuteAsync(null);
        await auto.SaveCommand.ExecuteAsync(null);
        Assert.True(auto.HasError);
        Assert.True(auto.Editor.IsOpen);
        Assert.Empty(await fixture.Automations.GetAllAsync(fixture.Token));
        var schedule = Schedule(fixture);
        await fixture.Automations.SaveAsync(schedule, fixture.Token);
        await auto.ReloadAsync();
        await auto.EditCommand.ExecuteAsync(Assert.Single(auto.Items));
        var stored = Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token));
        await fixture.Automations.SaveAsync(stored with { Name = "other-edit" }, fixture.Token);
        auto.Editor.Name = "stale-edit";
        await auto.SaveCommand.ExecuteAsync(null);
        Assert.True(auto.Editor.IsOpen);
        Assert.True(auto.HasError);
        Assert.Equal("other-edit", Assert.Single(await fixture.Automations.GetAllAsync(fixture.Token)).Name);
        auto.Dispose();
        Assert.False(auto.SaveCommand.CanExecute(null));
        Assert.False(auto.CreateCommand.CanExecute(null));
    }, TestContext.Current.CancellationToken);
}
