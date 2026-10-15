using Avalonia.Input;
using System.Text;
using MossAgent.App.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalPasteNativeTests
{
    private static bool HasRow(TerminalFrame frame, string text) => frame.Rows.Any(row =>
        string.Concat(row.Select(cell => cell.Text)).StartsWith(text, StringComparison.Ordinal));

    private static async Task<TerminalFrame> WaitAsync(TerminalVtNativeFixture fixture, Func<TerminalFrame, bool> predicate)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var frame = fixture.Store.Capture()!;
            if (predicate(frame)) return frame;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
        var current = fixture.Store.Capture()!;
        Assert.Fail($"win32={current.Win32InputMode}; bracketed={current.BracketedPaste}; state={fixture.Manager.GetSnapshot(fixture.Context.TaskId)?.State}\n"
            + string.Join("\n", current.Rows.Take(current.DisplayRows).Select(row => string.Concat(row.Select(cell => cell.Text)))));
        return current;
    }

    [Fact]
    public async Task BracketedUnicodePasteReachesRealConsoleConsumer()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        const string content = "00中e\u0301🚀\r\nnext";
        var expected = "\u001b[200~" + content.Replace("\r\n", "\n", StringComparison.Ordinal) + "\u001b[201~";
        var command = "$b=[Text.StringBuilder]::new(); [Console]::Write([char]27+'[?9001h'+[char]27+'[?2004h'); [Console]::WriteLine('PASTE-'+'READY'); 1.." + expected.Length
            + " | ForEach-Object { $null=$b.Append([Console]::ReadKey($true).KeyChar) }; [Console]::Write([char]27+'[?2004l'); [Console]::WriteLine(('PASTE-'+'RESULT:')+[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($b.ToString())))\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command })).IsSuccess);
        var ready = await WaitAsync(fixture, frame => HasRow(frame, "PASTE-READY") && frame.BracketedPaste);
        var paste = TerminalPasteEncoder.Encode(content, ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = paste, sessionId = ready.SessionId!.Value.ToString("D") })).IsSuccess);
        await WaitAsync(fixture, frame => HasRow(frame, "PASTE-RESULT:" + Convert.ToBase64String(Encoding.Unicode.GetBytes(expected))));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Fact]
    public async Task PlainUnicodePasteWaitsForExplicitEnterInPowerShell()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new
        { input = "[Console]::WriteLine('MULTI-'+'READY')\r" })).IsSuccess);
        var ready = await fixture.WaitFrameAsync(frame => HasRow(frame, "MULTI-READY") && !frame.BracketedPaste && frame.Win32InputMode);
        var paste = TerminalPasteEncoder.Encode("'MULTI-'+'book 1000 中e\u0301🚀'", ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = paste, sessionId = ready.SessionId!.Value.ToString("D") })).IsSuccess);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(HasRow(fixture.Store.Capture()!, "MULTI-book 1000 中e\u0301🚀"));
        var enter = TerminalKeyEncoder.Encode(new(ready.ViewId, ready.SessionId!.Value, Key: Key.Enter), ready);
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = enter })).IsSuccess);
        await fixture.WaitFrameAsync(frame => HasRow(frame, "MULTI-book 1000 中e\u0301🚀"));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }
}
