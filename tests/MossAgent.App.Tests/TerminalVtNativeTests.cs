using MossAgent.App.Terminal;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalVtNativeTests
{
    private static string Row(TerminalFrame frame, int index) => string.Concat(frame.Rows[index].Select(cell => cell.Text));

    [Fact]
    public async Task RealConPtyOutputReachesColoredCellsThroughApprovedToolsAndResetsOnRestart()
    {
        await using var fixture = new TerminalVtNativeFixture();
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var session = fixture.Manager.GetSnapshot(fixture.Context.TaskId)!.SessionId;
        var command = "[Console]::Write([char]27 + '[2J' + [char]27 + '[H' + [char]27 + '[31m' + ('VT-'+'红色') + [char]27 + '[0m' + [char]13 + [char]10)\r";
        Assert.True((await fixture.ExecuteAsync("terminal.write", new { input = command })).IsSuccess);
        var colored = await fixture.WaitFrameAsync(frame => frame.Rows.Any(row =>
            string.Concat(row.Select(cell => cell.Text)).StartsWith("VT-红色", StringComparison.Ordinal)
            && row[0].Foreground == 0xFFCD0000u));
        Assert.Equal(session, colored.SessionId);
        var index = Enumerable.Range(0, colored.Rows.Length).First(row => Row(colored, row).StartsWith("VT-红色", StringComparison.Ordinal));
        Assert.False(colored.Rows[index][0].DefaultForeground);
        Assert.Equal(2, colored.Rows[index][3].Width);
        Assert.False(colored.Failed);
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var restarted = await fixture.WaitFrameAsync(frame => frame.SessionId != session && frame.SessionId is not null);
        Assert.NotEqual(colored.ViewId, restarted.ViewId);
        Assert.DoesNotContain("VT-红色", string.Concat(restarted.Rows.SelectMany(row => row).Select(cell => cell.Text)));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }

    [Fact]
    public async Task ReadOnlyPolicyCannotStartShellOrBypassWriteApproval()
    {
        await using var fixture = new TerminalVtNativeFixture();
        var readOnly = fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        var denied = await fixture.ExecuteAsync("terminal.start", new { }, readOnly);
        Assert.False(denied.IsSuccess);
        Assert.Null(fixture.Manager.GetSnapshot(readOnly.TaskId));
        Assert.True((await fixture.ExecuteAsync("terminal.start", new { })).IsSuccess);
        var write = await fixture.ExecuteAsync("terminal.write", new { input = "'UNAUTHORIZED-VT-SENTINEL'\r" }, readOnly);
        Assert.False(write.IsSuccess);
        Assert.DoesNotContain("UNAUTHORIZED-VT-SENTINEL", string.Concat(fixture.Store.Capture()!.Rows.SelectMany(row => row).Select(cell => cell.Text)));
        Assert.True((await fixture.ExecuteAsync("terminal.stop", new { })).IsSuccess);
    }
}
