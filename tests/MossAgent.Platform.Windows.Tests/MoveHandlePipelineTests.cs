using MossAgent.Domain;
using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class MoveHandlePipelineTests
{
    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("denied", "approval_denied")]
    [InlineData("rootless", "path_denied")]
    public async Task DenialPrecedesNativeSessionAcquisition(string scenario, string error)
    {
        using var fixture = new MoveHandleFixture();
        var context = fixture.Context with
        {
            ApprovalPolicy = scenario == "readonly" ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime,
            AuthorizedRoots = scenario == "rootless" ? [] : [fixture.Authorized]
        };
        var result = await fixture.MoveAsync(context: context, approve: false);
        Assert.Equal(error, result.ErrorCode);
        Assert.Equal(0, fixture.Sessions.Creates);
        Assert.True(File.Exists(fixture.Source));
        Assert.False(File.Exists(fixture.Target));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationOrDeadlineAfterPreparationReleasesSource(bool deadline)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var time = new ManualToolTimeProvider();
        var sessions = new CallbackPathMoveSessions { AfterPrepare = () =>
        {
            if (deadline) time.Advance(TimeSpan.FromSeconds(2));
            else cancellation.Cancel();
        } };
        using var fixture = new MoveHandleFixture(sessions);
        var result = await fixture.MoveAsync(context: fixture.Context with { Timeout = TimeSpan.FromSeconds(1) },
            token: cancellation.Token, time: time);
        Assert.Equal(deadline ? "tool_timeout" : "tool_cancelled", result.ErrorCode);
        Assert.False(sessions.Last!.Moved);
        Assert.False(sessions.Last.FileContent!.CanRead);
        Assert.False(File.Exists(fixture.Target));
        await File.WriteAllTextAsync(fixture.Source, "released", fixture.Token);
    }

    [Theory]
    [InlineData("cancel", "tool_cancelled")]
    [InlineData("deadline", "tool_timeout")]
    [InlineData("revoke", "move_incomplete")]
    public async Task InterruptionAfterCreatingParentsPreservesKnownEffects(string scenario, string error)
    {
        using var fixture = new MoveHandleFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var time = new ManualToolTimeProvider();
        var roots = new List<string> { fixture.Authorized };
        var parent = Path.Combine(fixture.TargetParent, "new-parent");
        var context = fixture.Context with { AuthorizedRoots = roots, Timeout = TimeSpan.FromSeconds(1),
            WorkspaceGuard = new CallbackCopyWorkspaceGuard(() =>
            {
                if (!Directory.Exists(parent)) return;
                if (scenario == "cancel") cancellation.Cancel();
                if (scenario == "deadline") time.Advance(TimeSpan.FromSeconds(2));
                if (scenario == "revoke") roots.Clear();
            }) };
        var result = await fixture.MoveAsync(target: Path.Combine(parent, "target"), context: context, token: cancellation.Token, time: time);
        Assert.Equal(error, result.ErrorCode);
        Assert.Contains("新建 1 个", result.Summary);
        Assert.Contains("人工核实", result.Summary);
        Assert.True(File.Exists(fixture.Source));
        Assert.Empty(Directory.GetFileSystemEntries(parent));
    }

    [Fact]
    public async Task CancellingOnePreparedMoveDoesNotStopIndependentTask()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var sessions = new CallbackPathMoveSessions { AfterPrepare = () =>
        {
            if (Interlocked.Increment(ref calls) != 1) return;
            prepared.TrySetResult();
            Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).GetAwaiter().GetResult();
        } };
        using var fixture = new MoveHandleFixture(sessions);
        var first = fixture.MoveAsync(token: cancellation.Token);
        try
        {
            await prepared.Task.WaitAsync(fixture.Token);
            var secondSource = Path.Combine(fixture.SourceParent, "second.txt");
            var secondTarget = Path.Combine(fixture.TargetParent, "second.txt");
            await File.WriteAllTextAsync(secondSource, "independent task", fixture.Token);
            var second = await fixture.MoveAsync(secondSource, secondTarget,
                context: new(Guid.NewGuid(), fixture.Authorized, [fixture.Authorized], ApprovalPolicy.FullAccess));
            Assert.True(second.IsSuccess, second.Summary);
            Assert.Equal("independent task", await File.ReadAllTextAsync(secondTarget, fixture.Token));
        }
        finally { cancellation.Cancel(); }
        Assert.Equal("tool_cancelled", (await first.WaitAsync(fixture.Token)).ErrorCode);
        Assert.True(File.Exists(fixture.Source));
        Assert.False(File.Exists(fixture.Target));
    }

    [Fact]
    public async Task MoveSuccessObeysOutputLimit()
    {
        using var fixture = new MoveHandleFixture();
        var result = await fixture.MoveAsync(context: fixture.Context with { MaximumOutputCharacters = 24 });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.OutputCharacterCount <= 24);
        Assert.False(File.Exists(fixture.Source));
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Target, fixture.Token));
    }
}
