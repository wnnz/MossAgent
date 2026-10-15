using MossAgent.App.Terminal;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalGridProjectionTests
{
    [Fact]
    public void NewMetadataResizesGridWithoutClearingSessionContentOrViewIdentity()
    {
        using var store = new TerminalScreenStore(true);
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        var session = Guid.NewGuid();
        store.Attach(context, new(context.TaskId, session, 1, TerminalSessionState.Running, Grid: TerminalGridSize.Default));
        store.Append(new(context.TaskId, "preserved 中文", session));
        var before = store.Capture()!;
        Assert.True(store.ApplyState(new(context.TaskId, session, 2, TerminalSessionState.Running, Grid: new(40, 15))));
        var frame = store.Capture()!;
        Assert.Equal(40, frame.Columns);
        Assert.Equal(15, frame.ViewportRows);
        Assert.Equal(before.ViewId, frame.ViewId);
        Assert.Equal(session, frame.SessionId);
        Assert.Contains("preserved 中文", string.Concat(frame.Rows.SelectMany(row => row).Select(cell => cell.Text)));
        Assert.All(frame.Rows, row => Assert.Equal(40, row.Length));
        Assert.False(store.ApplyState(new(context.TaskId, session, 1, TerminalSessionState.Running, Grid: new(80, 25))));
        Assert.Equal(40, store.Capture()!.Columns);
    }

    [Theory]
    [InlineData(20, 5)]
    [InlineData(500, 200)]
    [InlineData(500, 5)]
    public void SupportedGeometryHasBoundedHistoryAndRestoresAlternateState(int columns, int rows)
    {
        using var screen = new TerminalVtScreen();
        screen.Resize(new(columns, rows));
        screen.Process("primary\u001b[?1049h\u001b[Halternate");
        screen.Resize(new(40, 15));
        var alternate = screen.Capture(Guid.Empty, Guid.Empty, null, false);
        Assert.True(alternate.AlternateScreen);
        Assert.Equal(40, alternate.Columns);
        Assert.Equal(15, alternate.ViewportRows);
        screen.Process("\u001b[?1049l");
        var primary = screen.Capture(Guid.Empty, Guid.Empty, null, false);
        Assert.False(primary.AlternateScreen);
        Assert.Contains("primary", string.Concat(primary.Rows.SelectMany(row => row).Select(cell => cell.Text)));
        Assert.InRange(primary.Rows.Length * primary.Columns, 1, TerminalVtScreen.MaximumCells);
        Assert.InRange(screen.EstimatedCells, 1, 2 * TerminalVtScreen.MaximumCells);
    }

    [Fact]
    public void LargeGridsEvictOlderScreensToRespectAggregateCellBudget()
    {
        using var store = new TerminalScreenStore(true);
        var first = Guid.NewGuid();
        for (var index = 0; index < 4; index++)
        {
            var task = index == 0 ? first : Guid.NewGuid();
            var context = new ToolExecutionContext(task, "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
            store.Attach(context, new(task, Guid.NewGuid(), index + 1, TerminalSessionState.Running, Grid: new(500, 200)));
        }
        Assert.False(store.Append(new(first, "evicted")));
        var frame = store.Capture()!;
        Assert.Equal(500, frame.Columns);
        Assert.Equal(200, frame.ViewportRows);
        Assert.Equal(100000, frame.Rows.Length * frame.Columns);
    }
}
