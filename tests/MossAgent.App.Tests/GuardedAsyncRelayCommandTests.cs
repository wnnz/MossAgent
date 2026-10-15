using CommunityToolkit.Mvvm.Input;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class GuardedAsyncRelayCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledCommand_RejectsSyncAndAsyncInvocation(bool generic)
    {
        var calls = 0;
        var command = CreateCommand(generic, _ => { calls++; return Task.CompletedTask; }, () => false);
        using var lifetime = (IDisposable)command;
        command.Execute(null);
        await command.ExecuteAsync(null);
        Assert.False(command.CanExecute(null));
        Assert.Equal(0, calls);
        Assert.Null(command.ExecutionTask);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunningCommand_ForwardsStateWithoutCancellingOnRejectedReentry(bool generic)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = CancellationToken.None;
        var command = CreateCommand(generic, token => { received = token; return release.Task; }, () => true);
        using var lifetime = (IDisposable)command;
        var properties = new List<string?>();
        var availabilityChanges = 0;
        command.PropertyChanged += (_, args) => properties.Add(args.PropertyName);
        command.CanExecuteChanged += (_, _) => availabilityChanges++;
        var running = command.ExecuteAsync(null);
        Assert.Same(running, command.ExecutionTask);
        Assert.True(command.IsRunning);
        Assert.True(command.CanBeCanceled);
        command.Execute(null);
        await command.ExecuteAsync(null);
        Assert.False(received.IsCancellationRequested);
        Assert.Contains(nameof(command.IsRunning), properties);
        Assert.True(availabilityChanges > 0);
        release.SetResult();
        await running;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_CancelsAndRejectsFutureCalls(bool generic)
    {
        var command = CreateCommand(generic, async token =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }, () => true);
        using var lifetime = (IDisposable)command;
        var running = command.ExecuteAsync(null);
        lifetime.Dispose();
        await running;
        Assert.True(command.IsCancellationRequested);
        Assert.False(command.CanExecute(null));
        Assert.False(command.CanBeCanceled);
        await command.ExecuteAsync(null);
        Assert.Same(running, command.ExecutionTask);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Notifications_PreserveCommandSenderAndAvailability(bool generic)
    {
        var enabled = false;
        var command = CreateCommand(generic, _ => Task.CompletedTask, () => enabled);
        using var lifetime = (IDisposable)command;
        var notifications = 0;
        command.CanExecuteChanged += (sender, _) =>
        {
            Assert.Same(command, sender);
            notifications++;
        };
        command.PropertyChanged += (sender, _) => Assert.Same(command, sender);

        enabled = true;
        command.NotifyCanExecuteChanged();
        Assert.True(command.CanExecute(null));
        Assert.Equal(1, notifications);
        command.Execute(null);
        Assert.Equal(2, notifications);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_IsIdempotentAndDetachesLateNotifications(bool generic)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CreateCommand(generic, _ => release.Task, () => true);
        using var lifetime = (IDisposable)command;
        var properties = 0;
        var availability = 0;
        command.PropertyChanged += (_, _) => properties++;
        command.CanExecuteChanged += (_, _) => availability++;
        var running = command.ExecuteAsync(null);

        lifetime.Dispose();
        var propertiesAfterDispose = properties;
        var availabilityAfterDispose = availability;
        lifetime.Dispose();
        release.SetResult();
        await running;

        Assert.Equal(propertiesAfterDispose, properties);
        Assert.Equal(availabilityAfterDispose, availability);
        Assert.False(command.IsRunning);
        Assert.False(command.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedExecution_PreservesExceptionAndAllowsRetry(bool generic)
    {
        var failure = new InvalidOperationException("synthetic command failure");
        var calls = 0;
        var command = CreateCommand(generic,
            _ => ++calls == 1 ? Task.FromException(failure) : Task.CompletedTask, () => true);
        using var lifetime = (IDisposable)command;

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => command.ExecuteAsync(null));
        Assert.Same(failure, observed);
        Assert.True(command.ExecutionTask!.IsFaulted);
        Assert.True(command.CanExecute(null));
        await command.ExecuteAsync(null);
        Assert.Equal(2, calls);
        Assert.True(command.ExecutionTask.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancel_LeavesExecutionOwnedUntilCallbackReturns(bool generic)
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = CancellationToken.None;
        var command = CreateCommand(generic, token => { received = token; return release.Task; }, () => true);
        using var lifetime = (IDisposable)command;
        var running = command.ExecuteAsync(null);

        command.Cancel();
        Assert.True(received.IsCancellationRequested);
        Assert.True(command.IsCancellationRequested);
        Assert.True(command.IsRunning);
        Assert.False(command.CanBeCanceled);
        Assert.False(command.CanExecute(null));
        Assert.Same(running, command.ExecutionTask);
        release.SetResult();
        await running;
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task GenericCommand_ForwardsTypedAndObjectParameters()
    {
        var received = new List<int>();
        using var command = new GuardedAsyncRelayCommand<int>(
            (value, _) => { received.Add(value); return Task.CompletedTask; }, value => value >= 0);
        IAsyncRelayCommand<int> typed = command;
        IAsyncRelayCommand untyped = command;

        await typed.ExecuteAsync(0);
        typed.Execute(1);
        await untyped.ExecuteAsync(2);
        untyped.Execute(3);
        await command.ExecuteAsync(4);
        command.Execute(5);
        await typed.ExecuteAsync(-1);
        await untyped.ExecuteAsync(-1);
        typed.Execute(-1);
        untyped.Execute(-1);

        Assert.Equal([0, 1, 2, 3, 4, 5], received);
        Assert.False(typed.CanExecute(-1));
        Assert.False(untyped.CanExecute(null));
    }

    [Fact]
    public async Task GenericCommand_NullReferenceParameterReachesPredicateAndCallback()
    {
        var received = new List<string?>();
        using var command = new GuardedAsyncRelayCommand<string>(
            (value, _) => { received.Add(value); return Task.CompletedTask; }, value => value is null);

        await command.ExecuteAsync((string?)null);
        await ((IAsyncRelayCommand)command).ExecuteAsync(null);
        Assert.Equal(new string?[] { null, null }, received);
        Assert.False(command.CanExecute("disabled"));
    }

    [Fact]
    public async Task GenericCommand_WrongObjectTypeThrowsBeforeExecutionUnlessDisposed()
    {
        var calls = 0;
        using var command = new GuardedAsyncRelayCommand<string>(
            (_, _) => { calls++; return Task.CompletedTask; }, _ => true);
        IAsyncRelayCommand untyped = command;

        Assert.Throws<ArgumentException>(() => untyped.CanExecute(42));
        Assert.Throws<ArgumentException>(() => untyped.Execute(42));
        await Assert.ThrowsAsync<ArgumentException>(() => untyped.ExecuteAsync(42));
        command.Dispose();
        Assert.False(untyped.CanExecute(42));
        untyped.Execute(42);
        await untyped.ExecuteAsync(42);
        await command.ExecuteAsync("disposed");
        command.Execute("disposed");
        Assert.False(command.CanExecute("disposed"));
        Assert.Equal(0, calls);
    }

    private static IAsyncRelayCommand CreateCommand(bool generic,
        Func<CancellationToken, Task> execute, Func<bool> canExecute) => generic
        ? new GuardedAsyncRelayCommand<string>((_, token) => execute(token), _ => canExecute())
        : new GuardedAsyncRelayCommand(execute, canExecute);
}
