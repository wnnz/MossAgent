using Avalonia.Input;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalPasteEncoderTests
{
    private static TerminalFrame Frame(bool win32 = false, bool bracketed = false)
    {
        using var screen = new TerminalVtScreen();
        return screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false) with { Win32InputMode = win32, BracketedPaste = bracketed };
    }

    [Theory]
    [InlineData(false, "A\rB\rC\rD")]
    [InlineData(true, "\u001b[200~A\nB\nC\nD\u001b[201~")]
    public void LineEndingsNormalizeAndBracketedPasteAddsExactlyOnePair(bool bracketed, string expected)
        => Assert.Equal(expected, TerminalPasteEncoder.Encode("A\r\nB\rC\nD", Frame(bracketed: bracketed)));

    [Fact]
    public void Win32PreservesUnicodeAndUsesEnterKeyForUnbracketedBreaks()
    {
        var frame = Frame(win32: true);
        var expected = string.Concat("中🚀\re\u0301".Select(character =>
        {
            var encoded = TerminalKeyEncoder.Encode(character == '\r' ? new(frame.ViewId, frame.SessionId!.Value, Key: Key.Enter)
                : new(frame.ViewId, frame.SessionId!.Value, Text: character.ToString()), frame)!;
            return encoded + encoded.Replace(";1;0;1_", ";0;0;1_", StringComparison.Ordinal);
        }));
        Assert.Equal(expected, TerminalPasteEncoder.Encode("中🚀\r\ne\u0301", frame));
    }

    [Fact]
    public void BracketedPasteUsesWin32PressAndReleaseRecordsWhenEnabled()
    {
        var result = TerminalPasteEncoder.Encode("中e\u0301🚀\r\nnext", Frame(win32: true, bracketed: true));
        Assert.StartsWith("\u001b[0;0;27;1;0;1_\u001b[0;0;27;0;0;1_", result);
        Assert.Contains("\u001b[0;0;10;1;0;1_\u001b[0;0;10;0;0;1_", result);
    }

    [Theory]
    [InlineData("\u001b[201~danger")]
    [InlineData("\0")]
    [InlineData("\u0003")]
    [InlineData("\u007f")]
    [InlineData("\u009b")]
    public void EmbeddedControlsCannotEscapePaste(string text) => Assert.Throws<ArgumentException>(() => TerminalPasteEncoder.Encode(text, Frame()));

    [Fact]
    public void InvalidUtf16IsRejected()
    {
        Assert.Throws<ArgumentException>(() => TerminalPasteEncoder.Encode("\ud800", Frame()));
        Assert.Throws<ArgumentException>(() => TerminalPasteEncoder.Encode("\udc00", Frame()));
    }

    [Theory]
    [InlineData(false, false, 32769)]
    [InlineData(false, true, 32768)]
    [InlineData(true, false, 3000)]
    public void EncodedLimitIncludesProtocolOverhead(bool win32, bool bracketed, int length)
        => Assert.Throws<ArgumentOutOfRangeException>(() => TerminalPasteEncoder.Encode(new string('x', length), Frame(win32, bracketed)));
}
