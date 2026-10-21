using MossAgent.Domain;
using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class CopyHandlePipelineTests
{
    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("denied", "approval_denied")]
    [InlineData("rootless", "path_denied")]
    public async Task DenialOccursBeforeNativeSourceSessionAcquisition(string scenario, string error)
    {
        using var fixture = new CopyHandleFixture();
        var context = fixture.Context with
        {
            ApprovalPolicy = scenario == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            AuthorizedRoots = scenario == "rootless" ? [] : [fixture.Authorized]
        };
        var result = await fixture.Executor(approve: false).ExecuteAsync(
            CopyHandleFixture.Request(fixture.Source, fixture.Destination), context, fixture.Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(0, fixture.Sessions.Creates);
        Assert.False(File.Exists(fixture.Destination));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrDeadlineAfterPrepareReleasesSourceAndDoesNotWriteTarget(bool deadline)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var time = new ManualToolTimeProvider();
        var sessions = new CallbackFileTreeSessions { Prepared = _ =>
        {
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
        } };
        using var fixture = new CopyHandleFixture(sessions);
        var result = await fixture.Executor(time: time).ExecuteAsync(CopyHandleFixture.Request(fixture.Source, fixture.Destination),
            fixture.Context with { Timeout = TimeSpan.FromSeconds(1) }, cancellation.Token);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.False(File.Exists(fixture.Destination));
        Assert.All(sessions.Last!.Entries, entry => Assert.True(entry.Stream is null || !entry.Stream.CanRead));
        await File.WriteAllTextAsync(fixture.Source, "released", fixture.Token);
    }

    [Theory]
    [InlineData("cancel", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoke", "copy_incomplete")]
    public async Task InterruptingAfterOneFilePreservesResultAndInvalidatesOnlyCommittedSnapshots(string scenario, string error)
    {
        using var fixture = new CopyHandleFixture();
        await File.WriteAllTextAsync(Path.Combine(fixture.SourceDirectory, "second.txt"), "second content", fixture.Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Authorized };
        var context = fixture.Context with { AuthorizedRoots = roots, Timeout = TimeSpan.FromSeconds(1),
            WorkspaceGuard = new CallbackCopyWorkspaceGuard(() =>
            {
                if (!Directory.EnumerateFiles(fixture.DestinationDirectory).Any()) return;
                if (scenario == "cancel") cancellation.Cancel();
                if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
                if (scenario == "revoke") roots.Clear();
            }) };
        var result = await fixture.Executor(time: time).ExecuteAsync(
            CopyHandleFixture.Request(fixture.SourceDirectory, fixture.DestinationDirectory), context, cancellation.Token);
        Assert.Equal(error, result.ErrorCode);
        Assert.Contains("已提交 1 个文件", result.Summary);
        Assert.Contains("人工核实", result.Summary);
        var committed = Assert.Single(Directory.GetFiles(fixture.DestinationDirectory));
        Assert.Null(context.FileSnapshots.Get(context.TaskId, MossAgent.Tools.BuiltIn.Files.FileSnapshotReader.CacheKey(committed)));
        Assert.Equal(2, Directory.GetFiles(fixture.SourceDirectory).Length);
        Assert.Empty(Directory.GetFiles(fixture.Authorized, ".mossagent-*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancelledPreparedCopyDoesNotBlockAnotherTaskSharingSource()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var sessions = new CallbackFileTreeSessions { Prepared = _ =>
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            prepared.TrySetResult();
            Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).GetAwaiter().GetResult();
        } };
        using var fixture = new CopyHandleFixture(sessions);
        var first = fixture.CopyAsync(token: cancellation.Token);
        await prepared.Task.WaitAsync(fixture.Token);
        var secondPath = Path.Combine(fixture.DestinationDirectory, "second-task.txt");
        var second = await fixture.CopyAsync(destination: secondPath, context: new(Guid.NewGuid(), fixture.Authorized,
            [fixture.Authorized], ApprovalPolicy.FullAccess));
        cancellation.Cancel();
        Assert.Equal("tool_cancelled", (await first.WaitAsync(fixture.Token)).ErrorCode);
        Assert.True(second.IsSuccess, second.Summary);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(secondPath, fixture.Token));
        Assert.False(File.Exists(fixture.Destination));
    }

    [Fact]
    public async Task SuccessfulCopyObeysStandardOutputLimit()
    {
        using var fixture = new CopyHandleFixture();
        var result = await fixture.CopyAsync(context: fixture.Context with { MaximumOutputCharacters = 24 });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.OutputCharacterCount <= 24);
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Destination, fixture.Token));
    }
}
