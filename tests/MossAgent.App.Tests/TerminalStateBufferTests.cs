using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalStateBufferTests
{
    private static TerminalSessionSnapshot Snapshot(Guid task, long revision) => new(task, Guid.NewGuid(), revision, TerminalSessionState.Running);

    [Fact]
    public void BurstKeepsOnlyNewestRevisionAndOnePendingCallback()
    {
        var callbacks = new Queue<Action>();
        var delivered = new List<TerminalSessionSnapshot>();
        using var buffer = new TerminalStateBuffer(callbacks.Enqueue, (_, snapshot) => delivered.Add(snapshot));
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        for (var revision = 1; revision <= 50_000; revision++) buffer.Append(Snapshot(task, revision));
        buffer.Append(Snapshot(task, 9));
        Assert.Single(callbacks);
        callbacks.Dequeue()();
        Assert.Equal(50_000, Assert.Single(delivered).Revision);
    }

    [Fact]
    public void SwitchingTaskReusesCallbackButDiscardsOldSnapshot()
    {
        var callbacks = new Queue<Action>();
        var delivered = new List<(int Generation, TerminalSessionSnapshot Snapshot)>();
        using var buffer = new TerminalStateBuffer(callbacks.Enqueue, (generation, snapshot) => delivered.Add((generation, snapshot)));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        buffer.Attach(first, 1);
        buffer.Append(Snapshot(first, 100));
        buffer.Attach(second, 2);
        buffer.Append(Snapshot(first, 101));
        var current = Snapshot(second, 1);
        buffer.Append(current);
        Assert.Single(callbacks);
        callbacks.Dequeue()();
        Assert.Equal((2, current), Assert.Single(delivered));
    }

    [Fact]
    public void StateArrivingDuringPublicationSchedulesNextBatch()
    {
        var callbacks = new Queue<Action>();
        var task = Guid.NewGuid();
        TerminalStateBuffer? buffer = null;
        var delivered = new List<TerminalSessionSnapshot>();
        using var owner = buffer = new TerminalStateBuffer(callbacks.Enqueue, (_, snapshot) =>
        {
            delivered.Add(snapshot);
            if (snapshot.Revision == 1) buffer!.Append(Snapshot(task, 2));
        });
        buffer.Attach(task, 1);
        buffer.Append(Snapshot(task, 1));
        callbacks.Dequeue()();
        Assert.Single(callbacks);
        callbacks.Dequeue()();
        Assert.Equal([1L, 2L], delivered.Select(snapshot => snapshot.Revision));
    }

    [Fact]
    public void DisposalRejectsQueuedAndFutureState()
    {
        var callbacks = new Queue<Action>();
        var delivered = 0;
        var buffer = new TerminalStateBuffer(callbacks.Enqueue, (_, _) => delivered++);
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        buffer.Append(Snapshot(task, 1));
        buffer.Dispose();
        buffer.Append(Snapshot(task, 2));
        callbacks.Dequeue()();
        Assert.Equal(0, delivered);
        Assert.Empty(callbacks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void FailedDispatcherSchedulingCanRetryOnNextState(long nextRevision)
    {
        var callbacks = new Queue<Action>();
        var calls = 0;
        TerminalSessionSnapshot? delivered = null;
        using var buffer = new TerminalStateBuffer(action =>
        { if (++calls == 1) throw new InvalidOperationException(); callbacks.Enqueue(action); }, (_, snapshot) => delivered = snapshot);
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        buffer.Append(Snapshot(task, 1));
        buffer.Append(Snapshot(task, nextRevision));
        callbacks.Dequeue()();
        Assert.Equal(nextRevision, delivered!.Revision);
    }
}
