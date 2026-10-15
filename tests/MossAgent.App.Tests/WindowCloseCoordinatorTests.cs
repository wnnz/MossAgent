using Avalonia.Headless;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WindowCloseCoordinatorTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(Avalonia.Application));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task CloseIsDeferredAndRepeatedRequestsStartOneShutdown() => _session.DispatchAsync(async () =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var coordinator = new WindowCloseCoordinator(window, state,
            () => { calls++; return release.Task; }, schedule: callback => scheduled.SetResult(callback));

        window.Close();
        window.Close();
        state.ForceExitCommand.Execute(null);
        state.RetryExitCommand.Execute(null);

        Assert.Equal(1, calls);
        Assert.True(window.IsVisible);
        Assert.True(state.IsPending);
        Assert.False(state.ForceExitCommand.CanExecute(null));
        Assert.False(state.RetryExitCommand.CanExecute(null));
        Assert.False(scheduled.Task.IsCompleted);
        release.SetResult();
        var finish = await scheduled.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(window.IsVisible);
        Assert.False(state.IsVisible);
        finish();
        Assert.False(window.IsVisible);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailureKeepsWindowOpenAndOnlyExplicitForceExitClosesIt() => _session.Dispatch(() =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var scheduled = new List<Action>();
        using var coordinator = new WindowCloseCoordinator(window, state,
            () => Task.FromException(new InvalidOperationException("Authorization: Bearer synthetic-secret")),
            schedule: scheduled.Add);

        window.Close();
        window.Close();

        Assert.True(window.IsVisible);
        Assert.True(state.HasError);
        Assert.DoesNotContain("synthetic-secret", state.Title + state.Message);
        Assert.Empty(scheduled);
        Assert.True(state.ForceExitCommand.CanExecute(null));
        state.ForceExitCommand.Execute(null);
        state.ForceExitCommand.Execute(null);
        Assert.Single(scheduled)();
        Assert.False(window.IsVisible);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposalRejectsLateCompletionAndRemovesCloseSubscriptions() => _session.DispatchAsync(async () =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new List<Action>();
        var coordinator = new WindowCloseCoordinator(window, state, () => release.Task, schedule: scheduled.Add);
        window.Close();

        coordinator.Dispose();
        coordinator.Dispose();
        release.SetResult();
        await Task.Yield();
        window.Close();

        Assert.False(window.IsVisible);
        Assert.Empty(scheduled);
        Assert.True(state.IsPending);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ExplicitRetry_SharesPendingCleanupAndClosesOnlyAfterSuccess() => _session.DispatchAsync(async () =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new TaskCompletionSource<Action>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var coordinator = new WindowCloseCoordinator(window, state,
            () => ++calls == 1 ? Task.FromException(new IOException("synthetic refusal")) : release.Task,
            schedule: callback => scheduled.SetResult(callback));
        window.Close();
        Assert.True(state.HasError);
        Assert.True(window.IsVisible);

        state.RetryExitCommand.Execute(null);
        state.RetryExitCommand.Execute(null);
        state.ForceExitCommand.Execute(null);
        window.Close();

        Assert.True(state.IsPending);
        Assert.False(state.RetryExitCommand.CanExecute(null));
        Assert.Equal(2, calls);
        Assert.False(scheduled.Task.IsCompleted);
        release.SetResult();
        var finish = await scheduled.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(state.IsVisible);
        state.RetryExitCommand.Execute(null);
        Assert.Equal(2, calls);
        finish();
        Assert.False(window.IsVisible);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task RepeatedFailures_KeepTheWindowOpenAndDisposalUnsubscribesBothActions() => _session.Dispatch(() =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var calls = 0;
        var scheduled = new List<Action>();
        using var coordinator = new WindowCloseCoordinator(window, state,
            () => { calls++; return Task.FromException(new IOException("Authorization: Bearer synthetic-secret")); },
            schedule: scheduled.Add);
        window.Close();
        state.RetryExitCommand.Execute(null);
        Assert.Equal(2, calls);
        Assert.True(state.HasError);
        Assert.True(window.IsVisible);
        Assert.Empty(scheduled);
        Assert.DoesNotContain("synthetic-secret", state.Message);
        coordinator.Dispose();
        state.RetryExitCommand.Execute(null);
        state.ForceExitCommand.Execute(null);
        Assert.Equal(2, calls);
        Assert.Empty(scheduled);
        window.Close();
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisposalDuringRetry_RejectsLateSuccessfulCompletion() => _session.DispatchAsync(async () =>
    {
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scheduled = new List<Action>();
        var calls = 0;
        using var coordinator = new WindowCloseCoordinator(window, state,
            () => ++calls == 1 ? Task.FromException(new IOException("synthetic refusal")) : release.Task,
            schedule: scheduled.Add);
        window.Close();
        state.RetryExitCommand.Execute(null);
        coordinator.Dispose();
        release.SetResult();
        await Task.Yield();
        window.Close();
        Assert.Equal(2, calls);
        Assert.False(window.IsVisible);
        Assert.Empty(scheduled);
        Assert.True(state.IsPending);
    }, TestContext.Current.CancellationToken);
}
