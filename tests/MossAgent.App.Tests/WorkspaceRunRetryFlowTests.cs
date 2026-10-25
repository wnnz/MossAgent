using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceRunRetryFlowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task NotSubmittedFailure_RetriesDirectlyWithTheSamePrompt() => _session.DispatchAsync(async () =>
    {
        var runner = new RetryProbeAgentRunner(
            [new AgentFailureEvent("provider_connection", "synthetic-secret")],
            [new AgentTextEvent("retried answer"), new AgentCompletedEvent()]);
        await using var fixture = await CreateFixtureAsync(runner);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 1);
        Assert.True(workspace.IsSelectedTaskFailed);
        Assert.True(workspace.RunRetry.IsAvailable);
        Assert.True(workspace.RunRetry.ShowsRequest);
        await workspace.RunRetry.RequestRetryCommand.ExecuteAsync(null);
        Assert.False(workspace.RunRetry.IsConfirmationVisible);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 2);
        Assert.Equal("合成请求，验证明确选择的模型。", LastUserContent(runner.Requests[1]));
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        var userMessages = (await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token))
            .Where(message => message.Role == MessageRole.User).ToArray();
        Assert.Equal(2, userMessages.Length);
        Assert.Equal(userMessages[0].Content, userMessages[1].Content);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PossiblySubmittedFailure_ResendsOnlyAfterExplicitConfirmation() => _session.DispatchAsync(async () =>
    {
        var runner = new RetryProbeAgentRunner(
            [new AgentFailureEvent("http_error", "synthetic-secret")],
            [new AgentTextEvent("retried answer"), new AgentCompletedEvent()]);
        await using var fixture = await CreateFixtureAsync(runner);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 1);
        await workspace.RunRetry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Single(runner.Requests);
        Assert.True(workspace.RunRetry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmPossiblySubmitted"), workspace.RunRetry.ConfirmationText);
        workspace.RunRetry.CancelRetryCommand.Execute(null);
        Assert.False(workspace.RunRetry.IsConfirmationVisible);
        await workspace.RunRetry.RequestRetryCommand.ExecuteAsync(null);
        await workspace.RunRetry.ConfirmRetryCommand.ExecuteAsync(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 2);
        Assert.Equal("合成请求，验证明确选择的模型。", LastUserContent(runner.Requests[1]));
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ToolsExecutedFailure_WarnsBeforeResending() => _session.DispatchAsync(async () =>
    {
        var runner = new RetryProbeAgentRunner(
            [new AgentToolEvent("files.read", "call-1", ToolResult.Success("read", "synthetic data")),
                new AgentFailureEvent("http_error", "synthetic-secret")],
            [new AgentCompletedEvent()]);
        await using var fixture = await CreateFixtureAsync(runner);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 1);
        await workspace.RunRetry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Single(runner.Requests);
        Assert.True(workspace.RunRetry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmToolsExecuted"), workspace.RunRetry.ConfirmationText);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UnknownFailure_KeepsUnknownStateBeforeResending() => _session.DispatchAsync(async () =>
    {
        var runner = new RetryProbeAgentRunner(
            [new AgentFailureEvent("run_failed", "synthetic-secret")],
            [new AgentCompletedEvent()]);
        await using var fixture = await CreateFixtureAsync(runner);
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => !workspace.IsRunning && runner.Requests.Count == 1);
        await workspace.RunRetry.RequestRetryCommand.ExecuteAsync(null);
        Assert.Single(runner.Requests);
        Assert.True(workspace.RunRetry.IsConfirmationVisible);
        Assert.Equal(WorkspaceText.Get("RunRetryConfirmUnknown"), workspace.RunRetry.ConfirmationText);
        Assert.False(workspace.RunRetry.ShowsRequest);
    }, TestContext.Current.CancellationToken);

    private static async Task<WorkspaceModelSelectionFixture> CreateFixtureAsync(RetryProbeAgentRunner runner) =>
        await WorkspaceModelSelectionFixture.CreateAsync(
            configure: services => services.AddSingleton<IAgentRunner>(runner));

    private static string LastUserContent(AgentRunRequest request) =>
        request.Messages.Last(message => message.Role == ModelRole.User).Content;
}
