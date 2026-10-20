using System.Net.Http;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFailureReasonTests
{
    [Theory]
    [InlineData("stream_line_limit", TaskFailureReason.StreamLineLimit)]
    [InlineData("invalid_api_key", TaskFailureReason.ProviderAuthentication)]
    [InlineData("provider_blocked", TaskFailureReason.ProviderBlocked)]
    [InlineData("output_limit", TaskFailureReason.ProviderOutputLimit)]
    [InlineData("proxy_authentication_failed", TaskFailureReason.ProxyAuthentication)]
    [InlineData("unsupported_reasoning_effort", TaskFailureReason.UnsupportedReasoningEffort)]
    [InlineData("attachment_history_unavailable", TaskFailureReason.AttachmentHistory)]
    [InlineData("model_context_capacity", TaskFailureReason.ModelContextCapacity)]
    [InlineData("Bearer synthetic-secret", TaskFailureReason.Unknown)]
    public async Task FailureReason_PersistsWithoutRawProviderMessagesOrCodes(string code, TaskFailureReason reason)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var presented = await RunAsync(fixture, state, new TaskFailureProbeRunner(code));
        var failure = Assert.Single(presented.OfType<AgentFailureEvent>());
        Assert.Equal(TaskFailureClassifier.SafeCode(code), failure.Code);
        Assert.Equal(WorkspaceFailureText.Get(reason), failure.Message);
        Assert.Equal(failure.Message, state.Activity);
        Assert.DoesNotContain("synthetic-secret", state.Activity, StringComparison.Ordinal);
        var restored = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(AgentTaskStatus.Failed, restored.Status);
        Assert.Equal(reason, restored.FailureReason);
        var messages = await fixture.Repository.GetMessagesAsync(restored.Id, fixture.Token);
        Assert.Equal("partial response", Assert.Single(messages, message => message.Role == MessageRole.Assistant).Content);
        Assert.DoesNotContain(messages, message => message.Content.Contains("synthetic-secret", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("connection", TaskFailureReason.ProviderConnection)]
    [InlineData("timeout", TaskFailureReason.ProviderTimeout)]
    [InlineData("remote-cancellation", TaskFailureReason.ProviderTimeout)]
    [InlineData("unknown", TaskFailureReason.Unknown)]
    [InlineData("proxy-authentication", TaskFailureReason.ProxyAuthentication)]
    [InlineData("model-context", TaskFailureReason.ModelContextCapacity)]
    public async Task ExceptionCategories_PersistWhileMessagesStaySafe(string kind, TaskFailureReason reason)
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        Exception exception = kind switch
        {
            "connection" => new HttpRequestException("Bearer synthetic-secret"),
            "proxy-authentication" => new HttpRequestException("Bearer synthetic-secret", null,
                System.Net.HttpStatusCode.ProxyAuthenticationRequired),
            "timeout" => new TimeoutException("Bearer synthetic-secret"),
            "remote-cancellation" => new OperationCanceledException("Bearer synthetic-secret"),
            "model-context" => new PinnedModelContextException(),
            _ => new InvalidOperationException("Bearer synthetic-secret")
        };
        await RunAsync(fixture, state, new TaskFailureProbeRunner("unused", exception));
        Assert.Equal(WorkspaceFailureText.Get(reason), state.Activity);
        Assert.Equal(reason, Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).FailureReason);
    }

    [Fact]
    public async Task ExplicitCancellation_DoesNotBecomeProviderTimeout()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        using var state = fixture.CreateState();
        var events = await RunAsync(fixture, state, new TaskFailureProbeRunner("unused", cancel: state.Cancel));
        Assert.Single(events.OfType<AgentCancelledEvent>());
        Assert.Empty(events.OfType<AgentFailureEvent>());
        var restored = Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token));
        Assert.Equal(AgentTaskStatus.Cancelled, restored.Status);
        Assert.Null(restored.FailureReason);
        Assert.Single(await fixture.Repository.GetMessagesAsync(restored.Id, fixture.Token), message => message.Role == MessageRole.Assistant);
    }

    [Fact]
    public async Task ManualResumeAndSuccessfulCompletion_ClearDiagnosticButPreserveHistory()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await fixture.Factory.CompleteAsync(fixture.Task, "partial", AgentTaskStatus.Failed,
            fixture.Token, TaskFailureReason.StreamEventLimit);
        var resumed = await fixture.Factory.ResumeAsync(failed, "retry", ApprovalPolicy.ReadOnly, fixture.Token);
        Assert.Null(resumed.FailureReason);
        Assert.Null(Assert.Single(await fixture.Repository.GetTasksAsync(fixture.Project.Id, fixture.Token)).FailureReason);
        var completed = await fixture.Factory.CompleteAsync(resumed, "completed", AgentTaskStatus.Completed,
            fixture.Token, TaskFailureReason.StreamEventLimit);
        Assert.Null(completed.FailureReason);
        Assert.Equal(new[] { "initial prompt", "partial", "retry", "completed" },
            (await fixture.Repository.GetMessagesAsync(completed.Id, fixture.Token)).Select(message => message.Content));
    }

    [Fact]
    public async Task SelectedFailureHint_NotifiesAndDoesNotFollowBackgroundTask()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var workspace = fixture.Workspace;
        await workspace.LoadAsync();
        var changes = new List<string?>();
        workspace.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var failed = fixture.Task with { Status = AgentTaskStatus.Failed, FailureReason = TaskFailureReason.StreamLineLimit };
        workspace.Catalog.UpsertTask(failed);
        Assert.True(workspace.IsSelectedTaskFailed);
        Assert.Equal(WorkspaceFailureText.Get(failed.FailureReason), workspace.TaskFailureHint);
        Assert.Contains(nameof(WorkspaceViewModel.TaskFailureHint), changes);
        var other = failed with { Id = Guid.NewGuid(), Status = AgentTaskStatus.Completed, FailureReason = null };
        workspace.Catalog.UpsertTask(other);
        workspace.Catalog.UpsertTask(failed with { FailureReason = TaskFailureReason.StreamEventLimit }, select: false);
        Assert.False(workspace.IsSelectedTaskFailed);
        Assert.Equal(WorkspaceText.Get("TaskFailureHint"), workspace.TaskFailureHint);
    }

    private static async Task<IReadOnlyList<AgentEvent>> RunAsync(
        WorkspacePersistenceFixture fixture, WorkspaceRunState state, IAgentRunner runner)
    {
        using var runs = new WorkspaceRunTracker();
        runs.Add(state);
        var presented = new List<AgentEvent>();
        await fixture.CreateController(runner, runs).RunAsync(state, fixture.Panels, (current, agentEvent) =>
        {
            current.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, current.Assistant, current.Messages,
                status => current.Activity = status, presented.Add);
            return Task.CompletedTask;
        }, (current, status) => current.Activity = status, _ => { }, () => { });
        Assert.False(runs.IsRunning(state.Task.Id));
        return presented;
    }

    [Fact]
    public void FailurePresentation_DoesNotLeakIntoToolSummaryOrObserver()
    {
        var assistant = new ChatMessageViewModel("MossAgent", "partial response");
        var tool = ChatMessageViewModel.CreateTool(new ToolConversationPayload("subagent.run", false, "running", null), "call", true);
        var messages = new System.Collections.ObjectModel.ObservableCollection<ChatMessageViewModel> { assistant, tool };
        AgentEvent? observed = null;
        WorkspaceAgentEventPresenter.ApplyEvent(new AgentFailureEvent("Bearer synthetic-secret", "Bearer synthetic-secret"),
            assistant, messages, _ => { }, agentEvent => observed = agentEvent);
        Assert.False(tool.IsToolRunning);
        Assert.Equal(WorkspaceText.Get("TaskFailureHint"), tool.Tool!.Summary);
        Assert.Equal("run_failed", tool.Tool.ErrorCode);
        var failure = Assert.IsType<AgentFailureEvent>(observed);
        Assert.Equal("run_failed", failure.Code);
        Assert.Equal(tool.Tool.Summary, failure.Message);
        Assert.Equal("partial response", assistant.Content);
    }
}
