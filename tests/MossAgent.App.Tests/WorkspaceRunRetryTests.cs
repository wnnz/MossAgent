using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceRunRetryTests
{
    [Fact]
    public async Task Availability_RequiresFailedTaskAndSendableState()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.ProviderHttp);
        AgentTask? selected = failed;
        var canSend = true;
        using var retry = CreateRetry(fixture, () => selected, () => canSend, (_, _) => Task.CompletedTask);
        Assert.True(retry.IsAvailable);
        Assert.True(retry.RequestRetryCommand.CanExecute(null));
        selected = failed with { Status = AgentTaskStatus.Completed, FailureReason = null };
        retry.NotifyStateChanged();
        Assert.False(retry.IsAvailable);
        selected = failed;
        canSend = false;
        retry.NotifyStateChanged();
        Assert.False(retry.IsAvailable);
    }

    [Fact]
    public async Task NotSubmitted_RetriesImmediatelyWithTheOriginalPrompt()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.ProviderConnection);
        var sends = new List<(AgentTask Task, string Prompt)>();
        using var retry = CreateRetry(fixture, () => failed, () => true,
            (task, prompt) => { sends.Add((task, prompt)); return Task.CompletedTask; });
        await retry.RequestRetryCommand.ExecuteAsync(null);
        var send = Assert.Single(sends);
        Assert.Equal(failed.Id, send.Task.Id);
        Assert.Equal("initial prompt", send.Prompt);
        Assert.False(retry.IsConfirmationVisible);
    }

    [Fact]
    public async Task PossiblySubmitted_RequiresConfirmationBeforeResending()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.ProviderHttp);
        var sends = new List<(AgentTask Task, string Prompt)>();
        using var retry = CreateRetry(fixture, () => failed, () => true,
            (task, prompt) => { sends.Add((task, prompt)); return Task.CompletedTask; });
        await retry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Empty(sends);
        Assert.True(retry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmPossiblySubmitted"), retry.ConfirmationText);
        Assert.False(retry.ShowsRequest);
        await retry.ConfirmRetryCommand.ExecuteAsync(null);
        var send = Assert.Single(sends);
        Assert.Equal("initial prompt", send.Prompt);
        Assert.False(retry.IsConfirmationVisible);
    }

    [Fact]
    public async Task ToolsExecuted_WarnsAboutSideEffectsRegardlessOfCode()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.ProviderConnection);
        var tool = new ConversationMessage(Guid.NewGuid(), failed.Id, MessageRole.Tool,
            WorkspaceToolMessageSerializer.Serialize(new AgentToolEvent("files.read", "call-1",
                ToolResult.Success("read", "synthetic data"))), DateTimeOffset.UtcNow, "call-1");
        await fixture.Repository.AppendMessageAsync(tool, fixture.Token);
        var sends = new List<(AgentTask Task, string Prompt)>();
        using var retry = CreateRetry(fixture, () => failed, () => true,
            (task, prompt) => { sends.Add((task, prompt)); return Task.CompletedTask; });
        await retry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Empty(sends);
        Assert.True(retry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmToolsExecuted"), retry.ConfirmationText);
        await retry.ConfirmRetryCommand.ExecuteAsync(null);
        Assert.Single(sends);
    }

    [Fact]
    public async Task UnknownProgress_KeepsTheUnknownStateAndAsksForConfirmation()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.Unknown);
        var sends = new List<(AgentTask Task, string Prompt)>();
        using var retry = CreateRetry(fixture, () => failed, () => true,
            (task, prompt) => { sends.Add((task, prompt)); return Task.CompletedTask; });
        await retry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Empty(sends);
        Assert.True(retry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmUnknown"), retry.ConfirmationText);
        retry.CancelRetryCommand.Execute(null);
        Assert.False(retry.IsConfirmationVisible);
        await retry.ConfirmRetryCommand.ExecuteAsync(null);
        Assert.Empty(sends);
    }

    [Fact]
    public async Task StaleConfirmation_DropsWhenSelectionChanges()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var failed = await FailAsync(fixture, TaskFailureReason.ProviderHttp);
        AgentTask? selected = failed;
        using var retry = CreateRetry(fixture, () => selected, () => true, (_, _) => Task.CompletedTask);
        await retry.RequestRetryCommand.ExecuteAsync(null);
        Assert.True(retry.IsConfirmationVisible);
        selected = failed with { Id = Guid.NewGuid() };
        retry.NotifyStateChanged();
        Assert.False(retry.IsConfirmationVisible);
    }

    [Fact]
    public async Task RetriedTurn_ResendsTheLastUserPromptOnlyOnce()
    {
        await using var fixture = await WorkspacePersistenceFixture.CreateAsync();
        var first = await FailAsync(fixture, TaskFailureReason.ProviderHttp);
        var resumed = await fixture.Factory.ResumeAsync(first, "second prompt", ApprovalPolicy.ReadOnly, fixture.Token);
        var failed = await fixture.Factory.CompleteAsync(resumed, "partial two", AgentTaskStatus.Failed,
            fixture.Token, TaskFailureReason.ProviderHttp);
        var sends = new List<string>();
        using var retry = CreateRetry(fixture, () => failed, () => true,
            (_, prompt) => { sends.Add(prompt); return Task.CompletedTask; });
        await retry.RequestRetryCommand.ExecuteAsync(null);
        await retry.ConfirmRetryCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "second prompt" }, sends);
    }

    private static async Task<AgentTask> FailAsync(WorkspacePersistenceFixture fixture, TaskFailureReason reason) =>
        await fixture.Factory.CompleteAsync(fixture.Task, "partial", AgentTaskStatus.Failed, fixture.Token, reason);

    private static WorkspaceRunRetryViewModel CreateRetry(WorkspacePersistenceFixture fixture,
        Func<AgentTask?> selected, Func<bool> canSend, Func<AgentTask, string, Task> send) =>
        new(fixture.Factory, selected, canSend, send);
}
