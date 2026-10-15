using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalVtScreenTests
{
    private static TerminalFrame Capture(TerminalVtScreen screen) => screen.Capture(Guid.Empty, Guid.Empty, null, false);
    private static string Row(TerminalFrame frame, int row) => string.Concat(frame.Rows[row].Select(cell => cell.Text));

    [Fact]
    public void CarriageReturnAndEraseUpdateCellsInsteadOfAppendingEscapeText()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("progress 10%\rOK\u001b[K");
        var frame = Capture(screen);
        Assert.Equal("OK", Row(frame, 0));
        Assert.Equal(2, frame.CursorColumn);
        Assert.Equal(1, frame.DisplayRows);
        Assert.True(frame.HasOutput);
    }

    [Fact]
    public void SplitCsiAndSgrPreserveColorAttributesAndResetIdentity()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("\u001b[3");
        screen.Process("1;1;3;4mA\u001b[0mB\u001b[38;2;17;34;51mC");
        var cells = Capture(screen).Rows[0];
        Assert.False(cells[0].DefaultForeground);
        Assert.Equal(0xFFCD0000u, cells[0].Foreground);
        Assert.Equal(7, cells[0].Attributes & 7);
        Assert.True(cells[0].Underline);
        Assert.True(cells[1].DefaultForeground);
        Assert.Equal(0xFF112233u, cells[2].Foreground);
    }

    [Fact]
    public void UnicodeGraphemesAndSplitSurrogatesRetainWideSpacerCells()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("中e\u0301\ud83d");
        screen.Process("\ude80");
        var frame = Capture(screen);
        Assert.Equal("中e\u0301🚀", Row(frame, 0));
        Assert.Equal(2, frame.Rows[0][0].Width);
        Assert.Equal(0, frame.Rows[0][1].Width);
        Assert.Equal(2, frame.Rows[0][3].Width);
        Assert.Equal(5, frame.CursorColumn);
    }

    [Fact]
    public void AlternateScreenAndCursorVisibilityRestorePrimaryContent()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("primary\u001b[?1049h\u001b[2J\u001b[Halternate\u001b[?25l");
        var alternate = Capture(screen);
        Assert.True(alternate.AlternateScreen);
        Assert.False(alternate.CursorVisible);
        Assert.Equal("alternate", Row(alternate, 0));
        Assert.Equal(30, alternate.Rows.Length);
        Assert.Equal(30, alternate.DisplayRows);
        screen.Process("\u001b[?1049l\u001b[?25h");
        var primary = Capture(screen);
        Assert.False(primary.AlternateScreen);
        Assert.True(primary.CursorVisible);
        Assert.Equal("primary", Row(primary, 0));
    }

    [Fact]
    public void HistoryIsBoundedAndPreviouslyPublishedCellsCannotMutate()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("original");
        var original = Capture(screen);
        screen.Process("\rnew\u001b[K\r\n" + string.Concat(Enumerable.Range(0, 1000).Select(index => $"line-{index}\r\n")));
        var frame = Capture(screen);
        Assert.Equal("original", Row(original, 0));
        Assert.InRange(frame.Rows.Length, 30, TerminalVtScreen.HistoryRows + TerminalVtScreen.ViewportRows);
        Assert.Contains("line-999", string.Concat(frame.Rows.SelectMany(row => row).Select(cell => cell.Text)));
        Assert.DoesNotContain("line-0", Row(frame, 0));
    }

    [Theory]
    [InlineData("\u001b]52;c;", "\a")]
    [InlineData("\u001b]8;;https://example.invalid/", "\u001b\\")]
    [InlineData("\u001bPq", "\u001b\\")]
    [InlineData("\u001b_Gf=100;", "\u001b\\")]
    [InlineData("\u001b^", "\u001b\\")]
    [InlineData("\u001bX", "\u001b\\")]
    [InlineData("\u009d52;", "\u009c")]
    public void OpaqueHostAndGraphicsPayloadsAreDiscardedAcrossChunks(string prefix, string suffix)
    {
        using var screen = new TerminalVtScreen();
        screen.Process("before" + prefix);
        for (var chunk = 0; chunk < 16; chunk++) screen.Process(new string('z', 65536));
        foreach (var character in suffix) screen.Process(character.ToString());
        screen.Process("after");
        var frame = Capture(screen);
        Assert.Equal("beforeafter", Row(frame, 0));
        Assert.Equal(30, frame.Rows.Length);
        Assert.False(frame.Failed);
    }

    [Theory]
    [InlineData("\u0018")]
    [InlineData("\u001a")]
    [InlineData("\u001b[31m")]
    public void CancelOrNewEscapeTerminatesDiscardedControlString(string cancellation)
    {
        using var screen = new TerminalVtScreen();
        screen.Process("\u001b]0;ignored" + cancellation + "visible");
        Assert.Equal("visible", Row(Capture(screen), 0));
    }

    [Fact]
    public void UnicodeCodepointsWhoseUtf8BytesResembleC1ControlsAreNotFiltered()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("Ýß😀\u009b31mZ");
        var frame = Capture(screen);
        Assert.Equal("Ýß😀Z", Row(frame, 0));
        Assert.False(frame.Rows[0][4].DefaultForeground);
    }

    [Fact]
    public void ExcessiveCsiParametersAndGraphemeSuffixesRemainBoundedAndRecover()
    {
        using var screen = new TerminalVtScreen();
        screen.Process("\u001b[" + new string(';', 100000) + "mX");
        Assert.Equal("X", Row(Capture(screen), 0));
        screen.Process(new string('\u0301', 10000));
        var frame = Capture(screen);
        Assert.InRange(frame.Rows[0][0].Text.Length, 1, 65);
        Assert.Equal(30, frame.Rows.Length);
        screen.Process("\u001b[0mZ");
        Assert.Equal("Z", Capture(screen).Rows[0][1].Text);
    }
}
