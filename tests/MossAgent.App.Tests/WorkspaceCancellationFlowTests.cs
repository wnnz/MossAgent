using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceCancellationFlowTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    public async Task StoppedRun_FinalizesCardsAndPersistsTerminalState(
        bool cancel, bool emitFinalResult, bool unexpectedCancellation)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            using var state = await CreateStateAsync(services, root, token);
            using var runs = new WorkspaceRunTracker();
            runs.Add(state);
            const string secret = "Authorization: Bearer synthetic-secret";
            Exception exception = cancel || unexpectedCancellation
                ? new OperationCanceledException(secret) : new InvalidOperationException(secret);
            var runner = new TerminalWorkspaceAgentRunner(() => { if (cancel) state.Cancel(); }, exception, emitFinalResult);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var factory = services.GetRequiredService<WorkspaceTaskFactory>();
            var controller = new WorkspaceRunController(new WorkspaceRunExecutor(repository, runner, factory), runs, factory);
            var presented = new List<AgentEvent>();
            var changed = 0;

            await controller.RunAsync(state, services.GetRequiredService<WorkspaceTaskPanelCoordinator>(),
                (current, agentEvent) =>
                {
                    presented.Add(agentEvent);
                    current.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, current.Assistant,
                        current.Messages, status => current.Activity = status, _ => { });
                    return Task.CompletedTask;
                }, (current, status) => current.Activity = status, _ => { }, () => changed++);

            var card = Assert.Single(state.Messages, message => message.ToolCallId == "stopped-call");
            Assert.False(card.IsToolRunning);
            Assert.Equal(cancel ? "已取消" : "失败", card.ToolStatus);
            Assert.DoesNotContain(secret, state.Activity);
            Assert.DoesNotContain(secret, card.Tool!.Summary);
            Assert.False(runs.IsRunning(state.Task.Id));
            Assert.Equal(1, changed);
            Assert.Equal(cancel, presented.OfType<AgentCancelledEvent>().Any());
            var saved = Assert.Single(await repository.GetTasksAsync(state.Task.ProjectId, token));
            Assert.Equal(cancel ? AgentTaskStatus.Cancelled : AgentTaskStatus.Failed, saved.Status);
            var history = await repository.GetMessagesAsync(state.Task.Id, token);
            Assert.Equal(emitFinalResult ? 1 : 0, history.Count(message => message.Role == MessageRole.Tool));
            if (emitFinalResult)
                Assert.Equal("已取消", WorkspaceConversationMapper.ToChatMessage(
                    Assert.Single(history, message => message.Role == MessageRole.Tool)).ToolStatus);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelledStream_PersistsTextAlreadyPresentedBeforeTerminalStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var token = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(token);
            using var state = await CreateStateAsync(services, root, token);
            using var runs = new WorkspaceRunTracker();
            runs.Add(state);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var factory = services.GetRequiredService<WorkspaceTaskFactory>();
            var runner = new TextThenCancellationAgentRunner(state.Cancel);
            var controller = new WorkspaceRunController(new WorkspaceRunExecutor(repository, runner, factory), runs, factory);

            await controller.RunAsync(state, services.GetRequiredService<WorkspaceTaskPanelCoordinator>(),
                (current, agentEvent) =>
                {
                    current.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, current.Assistant,
                        current.Messages, status => current.Activity = status, _ => { });
                    return Task.CompletedTask;
                }, (current, status) => current.Activity = status, _ => { }, () => { });

            var history = await repository.GetMessagesAsync(state.Task.Id, token);
            Assert.Equal("已经流出的部分回复。", Assert.Single(history, message => message.Role == MessageRole.Assistant).Content);
            Assert.Equal(AgentTaskStatus.Cancelled, Assert.Single(await repository.GetTasksAsync(state.Task.ProjectId, token)).Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<WorkspaceRunState> CreateStateAsync(
        ServiceProvider services, string root, CancellationToken cancellationToken)
    {
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", root, [root], DateTimeOffset.UtcNow);
        await services.GetRequiredService<IWorkspaceRepository>().SaveProjectAsync(project, cancellationToken);
        var task = await services.GetRequiredService<WorkspaceTaskFactory>()
            .CreateAsync(project, "检查改动", ApprovalPolicy.FullAccess, false, cancellationToken);
        var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1"), "fixture", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id, "fixture", "fixture", 8192, 1024,
            "off", null, null, false, false, true, true, "{}");
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel> { new("你", "检查改动"), assistant };
        return new WorkspaceRunState(task, project, provider, model, messages, assistant);
    }
}
