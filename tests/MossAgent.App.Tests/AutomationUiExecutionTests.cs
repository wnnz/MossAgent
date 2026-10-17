using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationUiExecutionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(true, 526)]
    [InlineData(false, 1280)]
    [InlineData(true, 1280)]
    public Task EditorAndRowActions_ReachSchedulerAndVisibleConversation(bool dark, int width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await AutomationUiWorkflowFixture.CreateAsync(dark, width);
        var draft = fixture.Core.Workspace.ComposerText;
        var model = fixture.Core.Workspace.SelectedModel;
        var schedule = await AutomationUiWorkflowInput.CreateAsync(fixture);
        await fixture.ActivateAsync(fixture.Page.ToggleCommand);
        Assert.True(Assert.Single(await fixture.Core.Automations.GetAllAsync(fixture.Token)).IsEnabled);
        await fixture.ActivateAsync(fixture.Page.ToggleCommand);
        Assert.False(Assert.Single(await fixture.Core.Automations.GetAllAsync(fixture.Token)).IsEnabled);
        await fixture.ActivateAsync(fixture.Page.RunNowCommand);
        await fixture.WaitUntilAsync(() => fixture.Core.Server.Requests.Count == 1 && fixture.Page.Runs.Count == 1
            && fixture.Page.Items[0].IsRunning);
        var run = Assert.Single(fixture.Page.Runs).Run;
        Assert.Equal(AutomationRunStatus.Running, run.Status);
        Assert.Equal(schedule.Id, run.ScheduleId);
        var request = Assert.Single(fixture.Core.Server.Requests);
        Assert.Equal(fixture.Core.AlternativeModel.ModelId, request.GetProperty("model").GetString());
        Assert.Equal("medium", request.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains(request.GetProperty("input").EnumerateArray(), message => message.GetProperty("role").GetString() == "user"
            && message.GetProperty("content").GetString() == schedule.Prompt);
        Assert.False(fixture.Page.RemoveCommand.CanExecute(fixture.Page.Items[0]));
        Assert.False(fixture.Page.RunNowCommand.CanExecute(fixture.Page.Items[0]));
        Assert.Equal(draft, fixture.Core.Workspace.ComposerText);
        Assert.Same(model, fixture.Core.Workspace.SelectedModel);
        Assert.Null(fixture.Core.Workspace.Catalog.SelectedTask);
        fixture.Capture("running");
        fixture.Core.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => fixture.Page.Runs.Count == 1 && fixture.Page.Runs[0].Run.Status == AutomationRunStatus.Completed);
        var stored = Assert.Single(await fixture.Core.Automations.GetRunsAsync(schedule.Id, 50, fixture.Token));
        Assert.Equal(AutomationRunStatus.Completed, stored.Status);
        Assert.Equal(run.TaskId, stored.TaskId);
        fixture.Capture("completed");
        await fixture.ActivateAsync(fixture.Page.OpenRunCommand);
        await fixture.WaitUntilAsync(() => fixture.Core.Workspace.Messages.Any(message => message.Content == "模型选择链路已完成。"));
        Assert.False(fixture.Core.Main.Pages.IsAutomationsOpen);
        Assert.Equal(stored.TaskId, fixture.Core.Workspace.Catalog.SelectedTask!.Id);
        Assert.Equal(AgentTaskStatus.Completed, fixture.Core.Workspace.Catalog.SelectedTask.Status);
        var answer = Assert.Single(fixture.Window.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
            control => control.IsEffectivelyVisible && control.Markdown == "模型选择链路已完成。");
        answer.BringIntoView();
        fixture.Flush();
        var point = answer.TranslatePoint(new Point(4, answer.Bounds.Height / 2), fixture.Window)!.Value;
        Assert.True(new Rect(fixture.Window.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(fixture.Window.InputHitTest(point));
        Assert.True(ReferenceEquals(answer, hit) || answer.IsVisualAncestorOf(hit));
        Assert.Single(fixture.Core.Server.Requests);
        fixture.Capture("conversation");
    }, TestContext.Current.CancellationToken);
}
