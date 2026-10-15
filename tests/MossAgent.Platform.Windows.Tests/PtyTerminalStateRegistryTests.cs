using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalStateRegistryTests
{
    [Fact]
    public void CompletedBudgetDoesNotEvictActiveOrFailedCleanupOwners()
    {
        var states = new PtyTerminalStateRegistry();
        var active = states.Record(Guid.NewGuid(), Guid.NewGuid(), TerminalSessionState.Running);
        var failed = states.Record(Guid.NewGuid(), Guid.NewGuid(), TerminalSessionState.CleanupFailed);
        var first = states.Record(Guid.NewGuid(), Guid.NewGuid(), TerminalSessionState.Exited, 0);
        TerminalSessionSnapshot latest = first;
        for (var index = 0; index < PtyTerminalStateRegistry.MaximumCompletedSnapshots; index++)
            latest = states.Record(Guid.NewGuid(), Guid.NewGuid(), TerminalSessionState.Stopped);
        Assert.Null(states.GetSnapshot(first.TaskId));
        Assert.Equal(latest, states.GetSnapshot(latest.TaskId));
        Assert.Equal(active, states.GetSnapshot(active.TaskId));
        Assert.Equal(failed, states.GetSnapshot(failed.TaskId));
    }

    [Fact]
    public void CompletedEvictionCannotRemoveNewerOwnerForSameTask()
    {
        var states = new PtyTerminalStateRegistry();
        var task = Guid.NewGuid();
        states.Record(task, Guid.NewGuid(), TerminalSessionState.Stopped);
        var running = states.Record(task, Guid.NewGuid(), TerminalSessionState.Running);
        for (var index = 0; index < PtyTerminalStateRegistry.MaximumCompletedSnapshots; index++)
            states.Record(Guid.NewGuid(), Guid.NewGuid(), TerminalSessionState.Stopped);
        Assert.Equal(running, states.GetSnapshot(task));
    }

    [Fact]
    public void PublicationSuppressesObsoleteRevisionAndRepeatingStateKeepsRevision()
    {
        var states = new PtyTerminalStateRegistry();
        var task = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var starting = states.Record(task, owner, TerminalSessionState.Starting);
        var running = states.Record(task, owner, TerminalSessionState.Running);
        var delivered = new List<TerminalSessionSnapshot>();
        states.StateChanged += (_, args) => delivered.Add(args.Snapshot);
        states.Publish(starting);
        states.Publish(running);
        Assert.Equal([running], delivered);
        Assert.Equal(running, states.Record(task, owner, TerminalSessionState.Running));
    }
}
