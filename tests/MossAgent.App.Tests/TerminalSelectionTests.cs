using MossAgent.App.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalSelectionTests
{
    private static TerminalFrame Frame(string text, int columns = 40)
    {
        using var screen = new TerminalVtScreen();
        screen.Resize(new(columns, 5));
        screen.Process(text);
        return screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);
    }

    [Fact]
    public void WholeWideGlyphAndGraphemeCopyWithoutContinuationDuplicates()
    {
        var selection = new TerminalSelection();
        selection.Observe(Frame("A中e\u0301🚀Z"));
        selection.Begin(0, 2);
        selection.Extend(0, 5);
        Assert.Equal("中e\u0301🚀", selection.GetText());
        Assert.True(selection.Contains(0, 1, 2));
        selection.Begin(0, 5);
        selection.Extend(0, 2);
        Assert.Equal("中e\u0301🚀", selection.GetText());
    }

    [Fact]
    public void SoftWrapsJoinButHardBreaksRetainNewlineAndTrimPadding()
    {
        var frame = Frame("abcdefghijklmnopqrstUVWXY\r\nnext", 20);
        Assert.True(frame.WrappedRows[0]);
        var selection = new TerminalSelection();
        selection.Observe(frame);
        selection.SelectAll();
        Assert.Equal("abcdefghijklmnopqrstUVWXY\nnext", selection.GetText());
        selection.Begin(0, 18);
        selection.Extend(1, 2);
        Assert.Equal("stUVW", selection.GetText());
    }

    [Fact]
    public void HistoryIsSelectableWithBoundedCoordinates()
    {
        var selection = new TerminalSelection();
        selection.Observe(Frame(string.Join("\r\n", Enumerable.Range(0, 12).Select(index => $"line {index}"))));
        selection.Begin(-1, -1);
        selection.Extend(1, 39);
        Assert.Equal("line 0\nline 1", selection.GetText());
        selection.Extend(int.MaxValue, int.MaxValue);
        Assert.EndsWith("line 11", selection.GetText());
    }

    [Fact]
    public void HiddenTextIsNotExtracted()
    {
        var selection = new TerminalSelection();
        selection.Observe(Frame("visible \u001b[8msecret\u001b[0m"));
        selection.SelectAll();
        Assert.Equal("visible", selection.GetText());
    }

    [Theory]
    [InlineData("view")]
    [InlineData("task")]
    [InlineData("session")]
    [InlineData("alternate")]
    [InlineData("columns")]
    [InlineData("rows")]
    [InlineData("failed")]
    public void OwnershipOrGeometryChangesClearSelection(string scenario)
    {
        var frame = Frame("original");
        var selection = new TerminalSelection();
        selection.Observe(frame);
        selection.SelectAll();
        var replacement = scenario switch
        {
            "view" => frame with { ViewId = Guid.NewGuid() },
            "task" => frame with { TaskId = Guid.NewGuid() },
            "session" => frame with { SessionId = Guid.NewGuid() },
            "alternate" => frame with { AlternateScreen = true },
            "columns" => frame with { Columns = 30 },
            "rows" => frame with { DisplayRows = 3 },
            _ => frame with { Failed = true }
        };
        selection.Observe(replacement);
        Assert.False(selection.HasSelection);
        Assert.Null(selection.GetText());
    }

    [Fact]
    public void SelectedContentMutationClearsButUnrelatedRowsDoNot()
    {
        var frame = Frame("original\r\nother");
        var selection = new TerminalSelection();
        selection.Observe(frame);
        selection.Begin(0, 0);
        selection.Extend(0, 7);
        var unrelated = frame with { Revision = 2, Rows = frame.Rows.SetItem(1, frame.Rows[1].SetItem(0, frame.Rows[1][0] with { Text = "N" })) };
        selection.Observe(unrelated);
        Assert.Equal("original", selection.GetText());
        selection.Observe(unrelated with { Rows = unrelated.Rows.SetItem(0, unrelated.Rows[0].SetItem(0, unrelated.Rows[0][0] with { Text = "X" })) });
        Assert.False(selection.HasSelection);
    }

    [Fact]
    public void OversizedGraphemeRejectsInsteadOfTruncatingOrSplittingUnicode()
    {
        var frame = Frame("x");
        frame = frame with { Rows = frame.Rows.SetItem(0, frame.Rows[0].SetItem(0, frame.Rows[0][0] with { Text = new string('x', 32769) })) };
        var selection = new TerminalSelection();
        selection.Observe(frame);
        selection.Begin(0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => selection.GetText());
    }
}
