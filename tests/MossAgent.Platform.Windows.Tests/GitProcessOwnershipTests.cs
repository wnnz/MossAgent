using MossAgent.Domain;
using MossAgent.Testing;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class GitProcessOwnershipTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WorktreeExitAndCancellationLeaveNoHookDescendants(bool infrastructure, bool cancel)
    {
        var token = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var fixture = new GitOwnershipFixture();
        await fixture.InitializeAsync(token);
        var execution = fixture.StartAsync(infrastructure, cancellation.Token);
        await fixture.BindDescendantAsync(token);
        Assert.False(fixture.Descendant!.HasExited);

        if (cancel) cancellation.Cancel();
        else await fixture.ReleaseAsync(token);
        if (infrastructure && cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
        else
        {
            await execution;
            if (fixture.ToolExecution is not null)
            {
                var result = await fixture.ToolExecution;
                if (cancel) Assert.Equal("tool_cancelled", result.ErrorCode);
                else Assert.True(result.IsSuccess, result.Summary);
                Assert.Equal(1, fixture.Approvals);
            }
        }

        Assert.True(fixture.Descendant.HasExited, "A Git hook descendant survived its owning command.");
        Assert.False(File.Exists(Path.Combine(fixture.Root, "late.txt")));
    }

    [Fact]
    public async Task ConcurrentGitCommandsDoNotShareProcessOwnership()
    {
        var token = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var first = new GitOwnershipFixture();
        await using var second = new GitOwnershipFixture();
        await first.InitializeAsync(token);
        await second.InitializeAsync(token);
        var firstExecution = first.StartAsync(false, cancellation.Token);
        var secondExecution = second.StartAsync(true, token);
        await first.BindDescendantAsync(token);
        await second.BindDescendantAsync(token);

        cancellation.Cancel();
        await firstExecution;
        Assert.True(first.Descendant!.HasExited);
        Assert.False(second.Descendant!.HasExited);
        await second.ReleaseAsync(token);
        await secondExecution;

        Assert.True(second.Descendant.HasExited);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutWaitsForRealGitHookDescendantCleanup(bool infrastructure)
    {
        var token = TestContext.Current.CancellationToken;
        var clock = new ManualToolTimeProvider();
        await using var fixture = new GitOwnershipFixture();
        await fixture.InitializeAsync(token);
        var execution = fixture.StartAsync(infrastructure, token, clock: clock);
        await fixture.BindDescendantAsync(token);

        clock.Advance(TimeSpan.FromSeconds(30));
        if (infrastructure) await Assert.ThrowsAsync<TimeoutException>(() => execution);
        else
        {
            await execution;
            Assert.Equal("tool_timeout", (await fixture.ToolExecution!).ErrorCode);
        }

        Assert.True(fixture.Descendant!.HasExited);
    }

    [Fact]
    public async Task ReadOnlyPolicyRejectsWorktreeCreationWithoutLaunchingTheHook()
    {
        var token = TestContext.Current.CancellationToken;
        await using var fixture = new GitOwnershipFixture();
        await fixture.InitializeAsync(token);

        await fixture.StartAsync(false, token, ApprovalPolicy.ReadOnly);

        Assert.False((await fixture.ToolExecution!).IsSuccess);
        Assert.Equal(0, fixture.Approvals);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "child.pid")));
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, "tool-worktree")));
    }
}
