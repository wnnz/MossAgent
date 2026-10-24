using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions.Files;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeTargetClaimTests
{
    [Fact]
    public async Task CreateClaimsExclusivelyVerifiesAndCompletes()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var factory = new TrackingWorktreeClaimFactory();
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()), targets: factory);
        var taskId = Guid.NewGuid();

        var target = await service.CreateAsync(fixture.Project, taskId, fixture.Token);

        var claim = Assert.Single(factory.Claims);
        Assert.Equal(target, claim.Path);
        Assert.Equal(1, claim.Verifications);
        Assert.True(claim.Disposed);
        Assert.True(Directory.Exists(target));
        Assert.Contains("worktree", await fixture.GitAsync(fixture.Repository, "worktree", "list"));
    }

    [Fact]
    public async Task ClaimFailureStopsBeforeGitAndLeavesNoTarget()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var before = await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain");
        var factory = new TrackingWorktreeClaimFactory(failCreate: true);
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()), targets: factory);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Paths.WorktreesPath, taskId.ToString("N"));

        await Assert.ThrowsAsync<IOException>(
            () => service.CreateAsync(fixture.Project, taskId, fixture.Token));

        Assert.Empty(factory.Claims);
        Assert.False(Directory.Exists(target));
        Assert.Equal(before, await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain"));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task IdentityMismatchAfterGitRetainsDirectoryAndReportsFailure()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var factory = new TrackingWorktreeClaimFactory(failVerify: true);
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(new FixtureTaskProcessLauncher()), targets: factory);
        var taskId = Guid.NewGuid();
        var target = Path.Combine(fixture.Paths.WorktreesPath, taskId.ToString("N"));

        await Assert.ThrowsAsync<GitWorktreeCreationException>(
            () => service.CreateAsync(fixture.Project, taskId, fixture.Token));

        var claim = Assert.Single(factory.Claims);
        Assert.Equal(1, claim.Verifications);
        Assert.True(Directory.Exists(target));
    }
}
