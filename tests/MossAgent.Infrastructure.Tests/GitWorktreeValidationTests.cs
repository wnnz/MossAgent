using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeValidationTests
{
    [Fact]
    public async Task ReadOnlyValidationAcceptsOwnedRegistrationAndNeverCreatesOtherApplicationFolders()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var owner = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, owner, fixture.Token);
        var before = await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain");
        await fixture.Service.ValidateAsync(fixture.Project, owner, target, fixture.Token);
        Assert.Equal(before, await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain"));
        Assert.False(Directory.Exists(fixture.Paths.ArtifactsPath));
        Assert.False(Directory.Exists(fixture.Paths.BrowserProfilesPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnotherTaskOrNonCanonicalPathIsRejectedBeforeNativeDiscovery(bool alias)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var owner = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, owner, fixture.Token);
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Must not launch."));
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ValidateAsync(fixture.Project, alias ? owner : Guid.NewGuid(),
            alias ? Path.Combine(target, "..", owner.ToString("N")) : target, fixture.Token));
        Assert.Empty(launcher.Requests);
        Assert.True(Directory.Exists(target));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task GuardRejectsChangedContextRootsAndRevalidatesReplacedFilesystemAfterEarlierSuccess()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var owner = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, owner, fixture.Token);
        var guard = new ManagedWorktreeWorkspaceGuard(fixture.Service, fixture.Project, owner, target, [target]);
        var context = new ToolExecutionContext(owner, target, [target], ApprovalPolicy.ReadOnly) { WorkspaceGuard = guard };
        await context.ValidateWorkspaceAsync(fixture.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => (context with { AuthorizedRoots = [fixture.Outside] }).ValidateWorkspaceAsync(fixture.Token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => (context with { WorkingDirectory = fixture.Outside }).ValidateWorkspaceAsync(fixture.Token));
        Directory.Move(target, Path.Combine(fixture.Root, "retained"));
        await fixture.JunctionAsync(target, fixture.Outside);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => context.ValidateWorkspaceAsync(fixture.Token));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task ValidationDeadlineWaitsForItsOwnedDiscoveryProcessAndRetainsTheDirectory()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var owner = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, owner, fixture.Token);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new StubTaskProcess { Wait = token => { entered.SetResult(); return Task.Delay(Timeout.InfiniteTimeSpan, token); } };
        var clock = new ManualToolTimeProvider();
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(new RecordingTaskProcessLauncher(() => process)), clock);
        var execution = service.ValidateAsync(fixture.Project, owner, target, fixture.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        clock.Advance(TimeSpan.FromSeconds(30));
        await Assert.ThrowsAsync<TimeoutException>(() => execution);
        Assert.True(process.Disposed);
        Assert.Equal(1, process.Terminations);
        Assert.True(Directory.Exists(target));
    }
}
