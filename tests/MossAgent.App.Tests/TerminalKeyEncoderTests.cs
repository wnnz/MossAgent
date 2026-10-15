using Avalonia.Input;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalKeyEncoderTests
{
    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None, false, "\r")]
    [InlineData(Key.Back, KeyModifiers.None, false, "\u007f")]
    [InlineData(Key.Back, KeyModifiers.Control, false, "\b")]
    [InlineData(Key.Tab, KeyModifiers.None, false, "\t")]
    [InlineData(Key.Tab, KeyModifiers.Shift, false, "\u001b[Z")]
    [InlineData(Key.Up, KeyModifiers.None, false, "\u001b[A")]
    [InlineData(Key.Up, KeyModifiers.None, true, "\u001bOA")]
    [InlineData(Key.Left, KeyModifiers.Control, true, "\u001b[1;5D")]
    [InlineData(Key.Home, KeyModifiers.None, true, "\u001bOH")]
    [InlineData(Key.End, KeyModifiers.None, false, "\u001b[F")]
    [InlineData(Key.Delete, KeyModifiers.None, false, "\u001b[3~")]
    [InlineData(Key.PageDown, KeyModifiers.Control | KeyModifiers.Shift, false, "\u001b[6;6~")]
    [InlineData(Key.F1, KeyModifiers.None, false, "\u001bOP")]
    [InlineData(Key.F12, KeyModifiers.None, false, "\u001b[24~")]
    [InlineData(Key.C, KeyModifiers.Control, false, "\u0003")]
    [InlineData(Key.OemPipe, KeyModifiers.Control, false, "\u001c")]
    [InlineData(Key.Space, KeyModifiers.Control, false, "\0")]
    [InlineData(Key.A, KeyModifiers.Alt, false, "\u001ba")]
    [InlineData(Key.A, KeyModifiers.None, false, null)]
    [InlineData(Key.A, KeyModifiers.Control | KeyModifiers.Alt, false, null)]
    [InlineData(Key.Enter, KeyModifiers.Meta, false, null)]
    [InlineData(Key.C, KeyModifiers.Control | KeyModifiers.Shift, false, null)]
    [InlineData(Key.V, KeyModifiers.Control | KeyModifiers.Shift, false, null)]
    public void ClassicKeysRespectModesAndLeavePrintableAndAltGrTextToComposition(Key key, KeyModifiers modifiers, bool application, string? expected)
    {
        using var screen = new TerminalVtScreen();
        var frame = screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false) with { ApplicationCursorKeys = application };
        Assert.Equal(expected, TerminalKeyEncoder.Encode(new(frame.ViewId, frame.SessionId!.Value, Key: key, Modifiers: modifiers), frame));
    }

    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None, "\u001b[13;0;13;1;0;1_")]
    [InlineData(Key.Back, KeyModifiers.None, "\u001b[8;0;8;1;0;1_")]
    [InlineData(Key.Left, KeyModifiers.Control, "\u001b[37;0;0;1;8;1_")]
    [InlineData(Key.C, KeyModifiers.Control, "\u001b[67;0;3;1;8;1_")]
    [InlineData(Key.Tab, KeyModifiers.Shift, "\u001b[9;0;9;1;16;1_")]
    public void Win32ModeEncodesVirtualKeyUnicodeAndControlState(Key key, KeyModifiers modifiers, string expected)
    {
        using var screen = new TerminalVtScreen();
        screen.Process("\u001b[?9001h");
        var frame = screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);
        Assert.True(frame.Win32InputMode);
        Assert.Equal(expected, TerminalKeyEncoder.Encode(new(frame.ViewId, frame.SessionId!.Value, Key: key, Modifiers: modifiers), frame));
    }

    [Fact]
    public void UnicodeCompositionIsBoundedAndSurrogatesRemainOrdered()
    {
        using var screen = new TerminalVtScreen();
        var frame = screen.Capture(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false);
        var input = new TerminalInputRequest(frame.ViewId, frame.SessionId!.Value, "红😀");
        Assert.Equal("红😀", TerminalKeyEncoder.Encode(input, frame));
        frame = frame with { Win32InputMode = true };
        Assert.Equal("\u001b[0;0;32418;1;0;1_\u001b[0;0;55357;1;0;1_\u001b[0;0;56832;1;0;1_", TerminalKeyEncoder.Encode(input, frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerminalKeyEncoder.Encode(input with { Text = new string('x', 32769) }, frame));
        Assert.Throws<ArgumentOutOfRangeException>(() => TerminalKeyEncoder.Encode(input with { Text = new string('x', 3000) }, frame));
    }
}
