using Avalonia.Headless;
using Avalonia.Input;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationUiBoundaryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(true, 526)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    public Task StopAndDelete_KeepHistoryWithoutRepeatingModelCalls(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await AutomationUiWorkflowFixture.CreateAsync(dark, width);
        await fixture.ActivateAsync(fixture.Page.CreateCommand);
        AutomationUiWorkflowInput.Type(fixture, AutomationText.Name, "无效草稿仍需保留");
        await fixture.ActivateAsync(fixture.Page.SaveCommand);
        Assert.True(fixture.Page.HasError);
        Assert.True(fixture.Page.Editor.IsOpen);
        Assert.Equal("无效草稿仍需保留", fixture.Page.Editor.Name);
        Assert.Empty(await fixture.Core.Automations.GetAllAsync(fixture.Token));
        Assert.Empty(fixture.Core.Server.Requests);
        fixture.Activate(fixture.Button(fixture.Page.CloseEditorCommand, AutomationText.Cancel));
        var schedule = await AutomationUiWorkflowInput.CreateAsync(fixture);
        await fixture.ActivateAsync(fixture.Page.RunNowCommand);
        await fixture.WaitUntilAsync(() => fixture.Core.Server.Requests.Count == 1 && fixture.Page.Items[0].IsRunning);
        Assert.False(fixture.Button(fixture.Page.RemoveCommand).IsEffectivelyEnabled);
        fixture.Activate(fixture.Button(fixture.Page.StopCommand));
        await fixture.WaitUntilAsync(() => fixture.Page.Runs.Count == 1 && fixture.Page.Runs[0].Run.Status == AutomationRunStatus.Cancelled);
        var run = Assert.Single(await fixture.Core.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Cancelled, run.Status);
        Assert.Equal(AgentTaskStatus.Cancelled, Assert.Single(await fixture.Core.Tasks.GetTasksAsync(schedule.ProjectId, fixture.Token)).Status);
        fixture.Capture("cancelled");
        await fixture.ActivateAsync(fixture.Page.OpenRunCommand);
        Assert.False(fixture.Core.Main.Pages.IsAutomationsOpen);
        Assert.Equal(run.TaskId, fixture.Core.Workspace.Catalog.SelectedTask!.Id);
        Assert.Single(fixture.Core.Server.Requests);
        fixture.OpenPage();
        await fixture.WaitUntilAsync(() => !fixture.Page.List.IsLoading && !fixture.Page.History.IsLoading);
        fixture.Activate(fixture.Button(fixture.Page.RemoveCommand));
        Assert.True(fixture.Page.IsDeleteOpen);
        AutomationUiWorkflowInput.Key(fixture, Key.Escape, PhysicalKey.Escape);
        Assert.False(fixture.Page.IsDeleteOpen);
        Assert.Single(await fixture.Core.Automations.GetAllAsync(fixture.Token));
        fixture.Activate(fixture.Button(fixture.Page.RemoveCommand));
        await fixture.ActivateAsync(fixture.Page.ConfirmDeleteCommand);
        Assert.False(fixture.Page.IsDeleteOpen);
        Assert.True(fixture.Page.IsEmpty);
        Assert.Empty(await fixture.Core.Automations.GetAllAsync(fixture.Token));
        Assert.Equal(run, Assert.Single(await fixture.Core.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token)));
        Assert.Equal(run.TaskId, Assert.Single(await fixture.Core.Tasks.GetTasksAsync(schedule.ProjectId, fixture.Token)).Id);
        Assert.Single(fixture.Core.Server.Requests);
        fixture.Capture("removed");
    }, TestContext.Current.CancellationToken);
}
