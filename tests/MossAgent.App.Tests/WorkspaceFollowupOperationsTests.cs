using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFollowupOperationsTests
{
    [Fact]
    public async Task PreparationIsRegisteredBeforeWaitingForTheSameTaskGateAndCancellationDoesNotAffectAnotherTask()
    {
        using var operations = new WorkspaceFollowupOperations();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = operations.RunAsync(first, false, _ => release.Task);
        var ran = false;
        var waiting = operations.RunAsync(first, true, _ => { ran = true; return Task.CompletedTask; });
        Assert.True(operations.IsPreparing(first));
        await operations.RunAsync(second, true, token => { token.ThrowIfCancellationRequested(); return Task.CompletedTask; });
        operations.CancelPreparation(first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(ran);
        release.SetResult();
        await holder;
        await operations.StopAsync();
        Assert.False(operations.IsPreparing(first));
        Assert.Throws<ObjectDisposedException>(() => { _ = operations.RunAsync(first, true, _ => Task.CompletedTask); });
    }

    [Fact]
    public async Task StopCancelsAndDrainsAllTaskOwnedPreparations()
    {
        using var operations = new WorkspaceFollowupOperations();
        var first = operations.RunAsync(Guid.NewGuid(), true, token => Task.Delay(Timeout.InfiniteTimeSpan, token));
        var second = operations.RunAsync(Guid.NewGuid(), true, token => Task.Delay(Timeout.InfiniteTimeSpan, token));
        await operations.StopAsync();
        Assert.True(first.IsCompleted && second.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
    }
}
