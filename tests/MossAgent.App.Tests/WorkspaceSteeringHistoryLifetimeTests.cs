using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceSteeringHistoryLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ShutdownDrainsHistoryReadAndQueuedRunFinalizationWithoutFailure(bool readFailsDuringStop) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        var workspace = fixture.Workspace;
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.Steering.History.IsLoading);
        var taskId = workspace.Catalog.SelectedTask!.Id;
        var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        probe.BeforeRead = async token =>
        {
            entered.TrySetResult();
            try { await blocked.Task.WaitAsync(token); }
            catch (OperationCanceledException) when (readFailsDuringStop) { throw new IOException("synthetic-history-secret"); }
        };
        var history = workspace.Steering.History;
        var refresh = history.RefreshCommand.ExecuteAsync(null);
        await entered.Task.WaitAsync(fixture.Token);
        var finalizing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        history.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(history.IsLoading) && history.IsLoading) finalizing.TrySetResult();
        };
        fixture.Server.ReleaseResponses();
        await finalizing.Task.WaitAsync(fixture.Token);
        Assert.False(workspace.HasActiveRun);
        Assert.False(refresh.IsCompleted);

        await workspace.ShutdownAsync().WaitAsync(fixture.Token);
        await refresh;

        Assert.False(history.IsLoading);
        Assert.False(history.HasNotice);
        Assert.Single(fixture.Server.Requests);
        Assert.Equal(AgentTaskStatus.Completed, (await fixture.Tasks.GetTasksAsync(
            workspace.Catalog.SelectedProject!.Id, fixture.Token)).Single(task => task.Id == taskId).Status);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("io")]
    [InlineData("cancellation")]
    [InlineData("disposed")]
    public Task ReadFailureIsVisibleAndExplicitRefreshCanRecoverWithoutProviderReplay(string failure) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await CreateAsync();
        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Workspace.Steering.History.IsLoading);
        var probe = Assert.IsType<SteeringAcceptanceProbe>(fixture.Steering);
        var history = fixture.Workspace.Steering.History;
        probe.BeforeRead = _ => throw failure switch
        {
            "cancellation" => new OperationCanceledException("synthetic-history-secret"),
            "disposed" => new ObjectDisposedException("synthetic-history-secret"),
            _ => new IOException("synthetic-history-secret")
        };

        await history.RefreshCommand.ExecuteAsync(null);

        Assert.True(history.HasNotice);
        Assert.Equal(WorkspaceText.Get("SteeringHistoryFailed"), history.Notice);
        Assert.DoesNotContain("synthetic-history-secret", history.Notice);
        Assert.True(history.RefreshCommand.CanExecute(null));
        probe.BeforeRead = null;
        await history.RefreshCommand.ExecuteAsync(null);
        Assert.False(history.HasNotice);
        Assert.Single(fixture.Server.Requests);
        fixture.Server.ReleaseResponses();
        await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning && !history.IsLoading);
    }, TestContext.Current.CancellationToken);

    private static Task<WorkspaceModelSelectionFixture> CreateAsync() => WorkspaceModelSelectionFixture.CreateAsync(
        pauseResponse: true, configure: services =>
        {
            services.AddSingleton<SqliteConversationSteeringRepository>();
            services.AddSingleton<IConversationSteeringRepository>(provider => new SteeringAcceptanceProbe(
                provider.GetRequiredService<SqliteConversationSteeringRepository>()));
        });
}
