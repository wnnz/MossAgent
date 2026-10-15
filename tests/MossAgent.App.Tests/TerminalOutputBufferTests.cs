using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalOutputBufferTests
{
    [Fact]
    public void BurstKeepsOneUiCallbackAndOnlyNewestBoundedText()
    {
        var queued = new Queue<Action>();
        var received = new List<(int Generation, string Text, bool Truncated)>();
        using var buffer = new TerminalOutputBuffer(queued.Enqueue, (generation, text, cut) => received.Add((generation, text, cut)));
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        for (var index = 0; index < 50_000; index++) buffer.Append(task, "0123456789");
        buffer.Append(task, "LATEST-中文🚀");
        Assert.Single(queued);
        Assert.Empty(received);
        queued.Dequeue()();
        var output = Assert.Single(received);
        Assert.Equal(1, output.Generation);
        Assert.True(output.Truncated);
        Assert.Equal(TerminalOutputBuffer.MaximumCharacters, output.Text.Length);
        Assert.EndsWith("LATEST-中文🚀", output.Text);
        Assert.Empty(queued);
    }

    [Fact]
    public void ResetReusesPendingCallbackWithoutPublishingOldTaskText()
    {
        var queued = new Queue<Action>();
        var received = new List<(int Generation, string Text)>();
        using var buffer = new TerminalOutputBuffer(queued.Enqueue, (generation, text, _) => received.Add((generation, text)));
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        buffer.Attach(first, 1);
        buffer.Append(first, "old");
        buffer.Attach(second, 2);
        buffer.Append(first, "foreign");
        buffer.Append(second, "new");
        Assert.Single(queued);
        queued.Dequeue()();
        Assert.Equal((2, "new"), Assert.Single(received));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TruncationNeverKeepsTheLowHalfOfACutSurrogatePair(bool split)
    {
        var queued = new Queue<Action>();
        var received = new List<(string Text, bool Cut)>();
        using var buffer = new TerminalOutputBuffer(queued.Enqueue, (_, text, cut) => received.Add((text, cut)));
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        if (split) buffer.Append(task, "\ud83d");
        buffer.Append(task, (split ? "\ude80" : "🚀") + new string('x', TerminalOutputBuffer.MaximumCharacters - 1));
        queued.Dequeue()();
        var output = Assert.Single(received);
        Assert.True(output.Cut);
        Assert.Equal(new string('x', TerminalOutputBuffer.MaximumCharacters - 1), output.Text);
    }

    [Fact]
    public void UntruncatedSurrogatePairCanSpanUiBatches()
    {
        var queued = new Queue<Action>();
        var text = string.Empty;
        using var buffer = new TerminalOutputBuffer(queued.Enqueue, (_, chunk, cut) => { Assert.False(cut); text += chunk; });
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        buffer.Append(task, "\ud83d");
        queued.Dequeue()();
        buffer.Append(task, "\ude80");
        queued.Dequeue()();
        Assert.Equal("🚀", text);
    }

    [Fact]
    public void OutputArrivingDuringPublicationSchedulesOnlyNextBatch()
    {
        var queued = new Queue<Action>();
        var text = string.Empty;
        var task = Guid.NewGuid();
        TerminalOutputBuffer? buffer = null;
        using var owner = buffer = new TerminalOutputBuffer(queued.Enqueue, (_, chunk, _) =>
        { text += chunk; if (chunk == "first") { buffer!.Append(task, "second"); buffer.Append(task, "third"); } });
        buffer.Attach(task, 1);
        buffer.Append(task, "first");
        queued.Dequeue()();
        Assert.Single(queued);
        queued.Dequeue()();
        Assert.Equal("firstsecondthird", text);
    }

    [Fact]
    public void DisposalAndDetachedContextRejectPendingAndFutureOutput()
    {
        var queued = new Queue<Action>();
        var publications = 0;
        using var buffer = new TerminalOutputBuffer(queued.Enqueue, (_, _, _) => publications++);
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        buffer.Append(task, "pending");
        buffer.Dispose();
        buffer.Attach(task, 2);
        buffer.Append(task, "after");
        queued.Dequeue()();
        Assert.Equal(0, publications);
        Assert.Empty(queued);
    }

    [Fact]
    public void DispatcherFailureDoesNotPermanentlyLoseSchedulingOwnership()
    {
        var queued = new Queue<Action>();
        var received = new List<(string Text, bool Cut)>();
        var fail = true;
        using var buffer = new TerminalOutputBuffer(action => { if (fail) throw new InvalidOperationException(); queued.Enqueue(action); },
            (_, text, cut) => received.Add((text, cut)));
        var task = Guid.NewGuid();
        buffer.Attach(task, 1);
        buffer.Append(task, "lost");
        fail = false;
        buffer.Append(task, "retained");
        Assert.Single(queued);
        queued.Dequeue()();
        Assert.Equal(("retained", true), Assert.Single(received));
    }
}
