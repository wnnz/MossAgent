using Avalonia.Input;
using MossAgent.App.Terminal;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalInteractionNativeTests
{
    private static bool HasRow(TerminalFrame frame, string prefix) => frame.Rows.Any(row =>
        string.Concat(row.Select(cell => cell.Text)).StartsWith(prefix, StringComparison.Ordinal));

    [Fact]
    public async Task AppliedGridMatchesChildConsoleGeometryThroughTools()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var session = fixture.Manager.GetSnapshot(fixture.Context.TaskId)!.SessionId;
        var result = await fixture.ExecuteAsync("terminal.resize", new { columns = 80, rows = 18, sessionId = session.ToString("D") });
        Assert.True(result.IsSuccess);
        Assert.Equal(new TerminalGridSize(80, 18), fixture.Manager.GetSnapshot(fixture.Context.TaskId)!.Grid);
        var resized = await fixture.WaitFrameAsync(frame => frame.Columns == 80 && frame.ViewportRows == 18);
        Assert.Equal(session, resized.SessionId);
        var command = "[Console]::WriteLine(('SIZE-'+'RESULT:') + [Console]::WindowWidth + 'x' + [Console]::WindowHeight)\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command, sessionId = session.ToString("D") })).IsSuccess);
        await fixture.WaitFrameAsync(frame => HasRow(frame, "SIZE-RESULT:80x18"));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Theory]
    [InlineData(Key.Up, KeyModifiers.None, "UpArrow:0:None")]
    [InlineData(Key.Left, KeyModifiers.Control, "LeftArrow:0:Control")]
    [InlineData(Key.Enter, KeyModifiers.None, "Enter:13:None")]
    public async Task Win32KeysReachRealReadKeyAsInputRecords(Key key, KeyModifiers modifiers, string expected)
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var command = "[Console]::Write([char]27 + '[?9001h'); [Console]::WriteLine('KEY-'+'READY'); $k = [Console]::ReadKey($true); [Console]::WriteLine(('KEY-'+'RESULT:') + $k.Key + ':' + [int]$k.KeyChar + ':' + $k.Modifiers)\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command })).IsSuccess);
        var ready = await fixture.WaitFrameAsync(frame => HasRow(frame, "KEY-READY") && frame.Win32InputMode);
        var encoded = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId!.Value, Key: key, Modifiers: modifiers), ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = encoded, sessionId = ready.SessionId.Value.ToString("D") })).IsSuccess);
        await fixture.WaitFrameAsync(frame => HasRow(frame, "KEY-RESULT:" + expected));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Fact]
    public async Task UnicodeTextAndEditingKeysWorkInRealPowerShellPrompt()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new
        { input = "[Console]::Write([char]27 + '[?9001h'); [Console]::WriteLine('SHELL-'+'READY')\r" })).IsSuccess);
        var ready = await fixture.WaitFrameAsync(frame => HasRow(frame, "SHELL-READY") && frame.Win32InputMode);
        var text = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId!.Value, "'INPUT-'+'A红X'"), ready);
        var left = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId.Value, Key: Key.Left), ready);
        var back = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId.Value, Key: Key.Back), ready);
        var replacement = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId.Value, "B"), ready);
        var enter = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId.Value, Key: Key.Enter), ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new
        { input = text + left + back + replacement + enter, sessionId = ready.SessionId.Value.ToString("D") })).IsSuccess);
        await fixture.WaitFrameAsync(frame => HasRow(frame, "INPUT-A红B"));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Fact]
    public async Task ControlCCancelsRealRunningCommandWithoutStoppingShell()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new
        { input = "[Console]::WriteLine('CANCEL-'+'READY'); Start-Sleep -Seconds 30; [Console]::WriteLine('CANCEL-'+'UNEXPECTED')\r" })).IsSuccess);
        var ready = await fixture.WaitFrameAsync(frame => HasRow(frame, "CANCEL-READY"));
        var encoded = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId!.Value, Key: Key.C, Modifiers: KeyModifiers.Control), ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = encoded, sessionId = ready.SessionId.Value.ToString("D") })).IsSuccess);
        await fixture.WaitFrameAsync(frame => string.Concat(frame.Rows[frame.CursorRow].Select(cell => cell.Text)).StartsWith("PS ", StringComparison.Ordinal));
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = "'CANCEL-'+'AFTER'\r" })).IsSuccess);
        var after = await fixture.WaitFrameAsync(frame => HasRow(frame, "CANCEL-AFTER"));
        Assert.False(HasRow(after, "CANCEL-UNEXPECTED"));
        Assert.Equal(ready.SessionId, fixture.Manager.GetSnapshot(fixture.Context.TaskId)!.SessionId);
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Fact]
    public async Task ChildOutputPreservesCombiningAndSupplementaryUnicode()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var command = "[Console]::WriteLine(('UNICODE-'+'RESULT:')+[char]0x4e2d+' '+[char]0x65+[char]0x301+' '+[char]::ConvertFromUtf32(0x1f680))\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command })).IsSuccess);
        var frame = await fixture.WaitFrameAsync(value => HasRow(value, "UNICODE-RESULT:"));
        Assert.True(HasRow(frame, "UNICODE-RESULT:中 é 🚀"), string.Join("\n", frame.Rows.Select(row => string.Concat(row.Select(cell => cell.Text)))));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }
}
