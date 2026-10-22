using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceQueueSteeringBoundaryRaceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task FinalCloseWinsLateSteeringWhileQueuedRunContinues() =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await CreateAsync();
            var workspace = fixture.Workspace;
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            var taskId = workspace.Catalog.SelectedTask!.Id;

            workspace.ComposerText = "queued boundary input";
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
            Assert.Single(workspace.Queue.Items);
            workspace.ComposerText = "late steering draft";
            await workspace.Drafts.FlushAsync();

            var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
            var closing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var finishClose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            probe.BeforeClose = async token =>
            {
                closing.TrySetResult();
                await finishClose.Task.WaitAsync(token);
            };

            fixture.Server.ReleaseResponses();
            await closing.Task.WaitAsync(fixture.Token);
            var steering = workspace.Steering.SubmitCommand.ExecuteAsync(null);
            await Task.Delay(25, fixture.Token);
            Assert.False(steering.IsCompleted);
            finishClose.TrySetResult();
            await steering.WaitAsync(fixture.Token);
            await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.Items.Count == 0
                && fixture.Server.Requests.Count == 2);

            var messages = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
            Assert.Single(messages, message => message.Role == MessageRole.User
                && message.Content == "queued boundary input");
            Assert.DoesNotContain(messages, message => message.Content == "late steering draft");
            Assert.Empty((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
            Assert.Equal("late steering draft", workspace.ComposerText);
            Assert.Equal(2, fixture.Server.Requests.Count);
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AcceptedSteeringRunsBeforeQueuedInputAndConsumesEachDraftOnce() =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await CreateAsync();
            var workspace = fixture.Workspace;
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            var taskId = workspace.Catalog.SelectedTask!.Id;

            workspace.ComposerText = "queued after steering";
            await workspace.Queue.EnqueueCommand.ExecuteAsync(null);
            Assert.Single(workspace.Queue.Items);
            workspace.ComposerText = "steering before queue";
            await workspace.Steering.SubmitCommand.ExecuteAsync(null);
            Assert.Empty(workspace.ComposerText);
            workspace.ComposerText = "foreground draft survives";
            await workspace.Drafts.FlushAsync();

            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => !workspace.IsRunning && workspace.Queue.Items.Count == 0
                && fixture.Server.Requests.Count == 3);

            var messages = await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token);
            Assert.Single(messages, message => message.Role == MessageRole.User
                && message.Content == "queued after steering");
            Assert.Single(messages, message => message.Role == MessageRole.User
                && message.Content == "steering before queue");
            Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary,
                Assert.Single((await fixture.Steering.GetInputsAsync(
                    taskId, long.MaxValue, 64, fixture.Token)).Items).State);
            Assert.Equal("foreground draft survives", workspace.ComposerText);
            Assert.Equal(3, fixture.Server.Requests.Count);
        }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync() =>
        WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true, withProject: false,
            configure: services =>
            {
                services.AddSingleton<SqliteConversationSteeringRepository>();
                services.AddSingleton<IConversationSteeringRepository>(provider =>
                    new SteeringAcceptanceProbe(provider.GetRequiredService<SqliteConversationSteeringRepository>()));
            });
}
