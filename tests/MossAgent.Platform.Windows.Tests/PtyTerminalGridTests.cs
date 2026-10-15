using System.Collections.Concurrent;
using System.Text;
using MossAgent.Platform.Windows.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyTerminalGridTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AppliedSizeIsRevisionedAndRetainedThroughCleanup()
    {
        var sizes = new ConcurrentQueue<(int Columns, int Rows)>();
        var connection = new PtyTestConnection { Resizing = (columns, rows) => sizes.Enqueue((columns, rows)) };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var before = manager.GetSnapshot(task)!;
        Assert.Equal(TerminalGridSize.Default, before.Grid);
        await manager.ResizeAsync(task, before.SessionId, Path.GetTempPath(), new(40, 15), Token);
        var applied = manager.GetSnapshot(task)!;
        Assert.Equal(new(40, 15), applied.Grid);
        Assert.True(applied.Revision > before.Revision);
        Assert.Equal(before.SessionId, applied.SessionId);
        Assert.Equal((40, 15), Assert.Single(sizes));
        manager.Resize(task, 70, 20);
        Assert.Equal(new(70, 20), manager.GetSnapshot(task)!.Grid);
        await manager.StopAsync(task);
        Assert.Equal(new(70, 20), manager.GetSnapshot(task)!.Grid);
    }

    [Fact]
    public async Task ResizeFailureCannotPublishUnappliedDimensions()
    {
        var connection = new PtyTestConnection { Resizing = (_, _) => throw new IOException("synthetic-secret") };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var before = manager.GetSnapshot(task)!;
        await Assert.ThrowsAsync<IOException>(() => manager.ResizeAsync(task, before.SessionId, Path.GetTempPath(), new(40, 15), Token));
        Assert.Equal(before, manager.GetSnapshot(task));
    }

    [Fact]
    public async Task StaleSessionOrDifferentScopeCannotWriteOrResizeReplacement()
    {
        var connections = new List<PtyTestConnection>();
        var resizes = new ConcurrentQueue<(int, int)>();
        await using var manager = new PtyTerminalSessionManager((_, output, _) =>
        {
            var connection = new PtyTestConnection { Resizing = (columns, rows) => resizes.Enqueue((columns, rows)) };
            connections.Add(connection);
            return Task.FromResult(new PtyTerminalSession(connection, new(), output));
        });
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var previous = manager.GetSnapshot(task)!;
        await manager.StopAsync(task);
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        var current = manager.GetSnapshot(task)!;
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.WriteAsync(task, previous.SessionId, Path.GetTempPath(), "OLD", Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.ResizeAsync(task, previous.SessionId, Path.GetTempPath(), new(40, 15), Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.WriteAsync(task, current.SessionId, Path.Combine(Path.GetTempPath(), "other"), "ESCAPED", Token));
        Assert.Empty(resizes);
        Assert.Equal(0, connections[1].WriterStream.Length);
        await manager.WriteAsync(task, current.SessionId, Path.GetTempPath(), "CURRENT", Token);
        Assert.Equal("CURRENT", Encoding.UTF8.GetString(((MemoryStream)connections[1].WriterStream).ToArray()));
    }

    [Fact]
    public async Task AlreadyCancelledResizeDoesNotTouchTheChild()
    {
        var calls = 0;
        var connection = new PtyTestConnection { Resizing = (_, _) => Interlocked.Increment(ref calls) };
        await using var manager = new PtyTerminalSessionManager((_, output, _) => Task.FromResult(new PtyTerminalSession(connection, new(), output)));
        var task = Guid.NewGuid();
        await manager.StartAsync(task, Path.GetTempPath(), Token);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => manager.ResizeAsync(task, manager.GetSnapshot(task)!.SessionId, Path.GetTempPath(), new(40, 15), cancelled.Token));
        Assert.Equal(0, calls);
        Assert.Equal(TerminalGridSize.Default, manager.GetSnapshot(task)!.Grid);
    }
}
