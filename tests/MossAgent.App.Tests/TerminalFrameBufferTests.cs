using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalFrameBufferTests
{
    [Fact]
    public void BurstsKeepOneCallbackAndOnlyLatestImmutableFrame()
    {
        var callbacks = new Queue<Action>();
        var publications = new List<(int Generation, TerminalFrame? Frame)>();
        using var screen = new TerminalVtScreen();
        screen.Process("frame");
        var initial = screen.Capture(Guid.NewGuid(), Guid.NewGuid(), null, false);
        using var buffer = new TerminalFrameBuffer(callbacks.Enqueue, (generation, frame) => publications.Add((generation, frame)));
        buffer.Attach(3);
        for (var revision = 0; revision < 1000; revision++) buffer.Append(3, initial with { Revision = revision });
        Assert.Single(callbacks);
        callbacks.Dequeue()();
        var published = Assert.Single(publications);
        Assert.Equal(3, published.Generation);
        Assert.Equal(999, published.Frame!.Revision);
    }

    [Fact]
    public void AttachAndDisposeRejectOldQueuedOrProducerFrames()
    {
        var callbacks = new Queue<Action>();
        var publications = new List<int>();
        using var buffer = new TerminalFrameBuffer(callbacks.Enqueue, (generation, _) => publications.Add(generation));
        buffer.Attach(1);
        buffer.Append(1, null);
        buffer.Attach(2);
        buffer.Append(1, null);
        buffer.Append(2, null);
        Assert.Single(callbacks);
        callbacks.Dequeue()();
        Assert.Equal([2], publications);
        buffer.Append(2, null);
        buffer.Dispose();
        callbacks.Dequeue()();
        buffer.Append(2, null);
        Assert.Empty(callbacks);
        Assert.Equal([2], publications);
    }
}
