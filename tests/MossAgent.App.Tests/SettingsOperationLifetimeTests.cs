using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SettingsOperationLifetimeTests
{
    [Fact]
    public void CallerCancellation_DoesNotCancelSiblingOperations()
    {
        using var lifetime = new SettingsOperationLifetime();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var first = lifetime.CreateOperation(caller.Token);
        using var second = lifetime.CreateOperation(TestContext.Current.CancellationToken);

        caller.Cancel();

        Assert.True(first.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        using var later = lifetime.CreateOperation(TestContext.Current.CancellationToken);
        Assert.False(later.IsCancellationRequested);
    }

    [Fact]
    public void CancelingAChild_DoesNotCancelItsCallerOrOwner()
    {
        using var lifetime = new SettingsOperationLifetime();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var first = lifetime.CreateOperation(caller.Token);
        using var second = lifetime.CreateOperation(caller.Token);

        first.Cancel();

        Assert.False(caller.IsCancellationRequested);
        Assert.False(second.IsCancellationRequested);
        using var later = lifetime.CreateOperation(caller.Token);
        Assert.False(later.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_CancelsAllOwnedOperationsButNotAnotherOwner()
    {
        using var firstOwner = new SettingsOperationLifetime();
        using var secondOwner = new SettingsOperationLifetime();
        using var first = firstOwner.CreateOperation(TestContext.Current.CancellationToken);
        using var nested = firstOwner.CreateOperation(first.Token);
        using var independent = secondOwner.CreateOperation(TestContext.Current.CancellationToken);

        firstOwner.Dispose();

        Assert.True(first.IsCancellationRequested);
        Assert.True(nested.IsCancellationRequested);
        Assert.False(independent.IsCancellationRequested);
        firstOwner.Dispose();
        Assert.Throws<ObjectDisposedException>(() => firstOwner.CreateOperation(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Dispose_LeavesChildTokensUsableUntilTheOperationOwnerReleasesThem()
    {
        using var lifetime = new SettingsOperationLifetime();
        using var operation = lifetime.CreateOperation(TestContext.Current.CancellationToken);
        var token = operation.Token;

        lifetime.Dispose();

        Assert.Equal(token, operation.Token);
        Assert.True(token.IsCancellationRequested);
        var invoked = false;
        using var registration = token.Register(() => invoked = true);
        Assert.True(invoked);
    }

    [Fact]
    public void OwnerToken_RemainsStableAndCanceledAfterDisposal()
    {
        using var lifetime = new SettingsOperationLifetime();
        var token = lifetime.Token;
        using var operation = lifetime.CreateOperation(TestContext.Current.CancellationToken);

        lifetime.Dispose();

        Assert.Equal(token, lifetime.Token);
        Assert.True(token.IsCancellationRequested);
        Assert.True(operation.IsCancellationRequested);
        var invoked = false;
        using var registration = token.Register(() => invoked = true);
        Assert.True(invoked);
    }

    [Fact]
    public async Task OperationDeadline_DoesNotCancelTheCallerOrFutureOperations()
    {
        using var lifetime = new SettingsOperationLifetime(TimeSpan.FromMilliseconds(80));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var operation = lifetime.CreateOperation(caller.Token);
        var expired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = operation.Token.Register(() => expired.TrySetResult());

        await expired.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(operation.IsCancellationRequested);
        Assert.False(caller.IsCancellationRequested);
        using var later = lifetime.CreateOperation(caller.Token);
        Assert.False(later.IsCancellationRequested);
    }

    [Fact]
    public void InfiniteDeadline_StillHonorsCallerCancellation()
    {
        using var lifetime = new SettingsOperationLifetime(Timeout.InfiniteTimeSpan);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using var operation = lifetime.CreateOperation(caller.Token);

        Assert.False(operation.IsCancellationRequested);
        caller.Cancel();

        Assert.True(operation.IsCancellationRequested);
    }

    [Fact]
    public void PreCanceledCaller_ProducesACanceledOperation()
    {
        using var lifetime = new SettingsOperationLifetime();
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        caller.Cancel();

        using var operation = lifetime.CreateOperation(caller.Token);

        Assert.True(operation.IsCancellationRequested);
    }

    [Theory]
    [InlineData(-2L)]
    [InlineData(4294967295L)]
    public void InvalidDeadline_FailsWithoutCancelingTheCaller(long milliseconds)
    {
        using var lifetime = new SettingsOperationLifetime(TimeSpan.FromMilliseconds(milliseconds));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        Assert.Throws<ArgumentOutOfRangeException>(() => lifetime.CreateOperation(caller.Token));

        Assert.False(caller.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_RejectsFurtherOperationsEvenWhenCancellationObserverThrows()
    {
        using var lifetime = new SettingsOperationLifetime();
        using var operation = lifetime.CreateOperation(TestContext.Current.CancellationToken);
        using var registration = operation.Token.Register(() => throw new InvalidOperationException("fixture-observer-error"));

        Assert.Throws<AggregateException>(() => lifetime.Dispose());

        Assert.True(operation.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => lifetime.CreateOperation(TestContext.Current.CancellationToken));
        lifetime.Dispose();
    }
}
