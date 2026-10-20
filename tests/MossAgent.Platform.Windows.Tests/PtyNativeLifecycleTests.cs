using System.Collections.Concurrent;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyNativeLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopOrDisposalReapsRealShellAndItsBackgroundChild(bool dispose)
    {
        await using var fixture = new PtyNativeFixture();
        var task = Guid.NewGuid();
        await fixture.CreateChildScriptAsync();
        await fixture.StartAsync(task);
        await fixture.WriteAsync(task, PtyNativeFixture.PublishPid("parent.pid") + "; " + PtyNativeFixture.LaunchChild);
        var parent = await fixture.BindAsync("parent.pid");
        var child = await fixture.BindAsync("child.pid");
        Assert.False(parent.HasExited);
        Assert.False(child.HasExited);

        if (dispose) await fixture.Manager.DisposeAsync().AsTask().WaitAsync(PtyNativeFixture.Deadline, TestContext.Current.CancellationToken);
        else await fixture.StopAsync(task);

        await PtyNativeFixture.AssertExitedAsync(parent);
        await PtyNativeFixture.AssertExitedAsync(child);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "late.txt")));
    }

    [Fact]
    public async Task NaturalShellExitAutomaticallyReclaimsChildAndCanBeRestarted()
    {
        await using var fixture = new PtyNativeFixture();
        var task = Guid.NewGuid();
        var output = new ConcurrentQueue<string>();
        fixture.Manager.OutputReceived += (_, args) => { if (args.TaskId == task) output.Enqueue(args.Output); };
        await fixture.CreateChildScriptAsync();
        await fixture.StartAsync(task);
        await fixture.WriteAsync(task, PtyNativeFixture.PublishPid("first.pid") + "; " + PtyNativeFixture.LaunchChild);
        var first = await fixture.BindAsync("first.pid");
        var child = await fixture.BindAsync("child.pid");
        await fixture.WriteAsync(task, "[Console]::WriteLine('FINAL-'+'OUTPUT'); exit 23");
        await PtyNativeFixture.AssertExitedAsync(first);
        var exited = await WaitForExitSnapshotAsync(fixture, task);
        Assert.Equal(23, exited.ExitCode);
        await PtyNativeFixture.AssertExitedAsync(child);
        Assert.Contains("FINAL-OUTPUT", string.Concat(output));

        await fixture.StartAsync(task);
        Assert.NotEqual(exited.SessionId, fixture.Manager.GetSnapshot(task)!.SessionId);
        await fixture.WriteAsync(task, PtyNativeFixture.PublishPid("second.pid"));
        var second = await fixture.BindAsync("second.pid");
        Assert.False(second.HasExited);
        await fixture.WriteAsync(task, "[IO.File]::WriteAllText('executed.txt', '中文🚀'); [IO.File]::WriteAllText('executed.txt.ready', '')");
        Assert.Equal("中文🚀", await fixture.ReadPublishedFileAsync("executed.txt"));
        await fixture.StopAsync(task);
        await PtyNativeFixture.AssertExitedAsync(second);
    }

    private static async Task<TerminalSessionSnapshot> WaitForExitSnapshotAsync(PtyNativeFixture fixture, Guid task)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(PtyNativeFixture.Deadline);
        while (true)
        {
            var snapshot = fixture.Manager.GetSnapshot(task);
            if (snapshot?.State == TerminalSessionState.Exited) return snapshot;
            await Task.Delay(20, cancellation.Token);
        }
    }

    [Fact]
    public async Task StoppingOneTaskDoesNotKillAnotherTasksRealShell()
    {
        await using var fixture = new PtyNativeFixture();
        var firstTask = Guid.NewGuid();
        var secondTask = Guid.NewGuid();
        await fixture.CreateChildScriptAsync();
        await Task.WhenAll(fixture.StartAsync(firstTask), fixture.StartAsync(secondTask));
        await fixture.WriteAsync(firstTask, PtyNativeFixture.PublishPid("first.pid") + "; " + PtyNativeFixture.LaunchChild);
        await fixture.WriteAsync(secondTask, PtyNativeFixture.PublishPid("second.pid"));
        var first = await fixture.BindAsync("first.pid");
        var child = await fixture.BindAsync("child.pid");
        var second = await fixture.BindAsync("second.pid");

        await fixture.StopAsync(firstTask);
        await PtyNativeFixture.AssertExitedAsync(first);
        await PtyNativeFixture.AssertExitedAsync(child);
        Assert.False(second.HasExited);
        await fixture.WriteAsync(secondTask, "[IO.File]::WriteAllText('retained.txt', 'retained'); [IO.File]::WriteAllText('retained.txt.ready', '')");
        Assert.Equal("retained", await fixture.ReadPublishedFileAsync("retained.txt"));
        await fixture.StopAsync(secondTask);
        await PtyNativeFixture.AssertExitedAsync(second);
    }
}
