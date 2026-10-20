using MossAgent.Application.Git;
using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeReferenceTests
{
    [Theory]
    [InlineData("missing-base")]
    [InlineData("HEAD^{tree}")]
    [InlineData("--help")]
    [InlineData("HEAD\n--help")]
    [InlineData("HEAD wrong")]
    [InlineData("HEAD;whoami")]
    public async Task InvalidReferenceRefusesCreationWithoutChangingRepositoryOrOutsideFiles(string reference)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var before = await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain");
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<GitWorktreeReferenceException>(() => fixture.Service.CreateAsync(fixture.Project, id, fixture.Token, reference));
        Assert.False(Directory.Exists(fixture.Target(id)));
        Assert.Equal(before, await fixture.GitAsync(fixture.Repository, "worktree", "list", "--porcelain"));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task OversizedReferenceRefusesCreation()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        await Assert.ThrowsAsync<GitWorktreeReferenceException>(() => fixture.Service.CreateAsync(
            fixture.Project, Guid.NewGuid(), fixture.Token, new string('a', 513)));
        Assert.False(Directory.Exists(fixture.Paths.WorktreesPath));
    }

    [Fact]
    public async Task EmptyReferenceRetainsHeadAndCancelledSelectionCreatesNothing()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var head = await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD");
        var path = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token, "");
        Assert.Equal(head, await fixture.GitAsync(path, "rev-parse", "HEAD"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var id = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.CreateAsync(fixture.Project, id, cancelled.Token, "main"));
        Assert.False(Directory.Exists(fixture.Target(id)));
    }

    [Fact]
    public async Task MovingBranchAfterResolutionCannotReplaceSelectedCommit()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var original = await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD");
        await fixture.GitAsync(fixture.Repository, "branch", "selected-base", original);
        await fixture.GitAsync(fixture.Repository, "commit", "--allow-empty", "-m", "later baseline");
        var later = await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD");
        var changed = false;
        var launcher = new GitWorktreeObservingLauncher(new FixtureTaskProcessLauncher())
        {
            AfterExit = async request =>
            {
                if (!request.Arguments.Contains("selected-base^{commit}")) return;
                await fixture.GitAsync(fixture.Repository, "branch", "-f", "selected-base", later);
                changed = true;
            }
        };
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        var path = await service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token, "selected-base");
        Assert.True(changed);
        Assert.Equal(original, await fixture.GitAsync(path, "rev-parse", "HEAD"));
        Assert.Equal(later, await fixture.GitAsync(fixture.Repository, "rev-parse", "selected-base"));
        var add = Assert.Single(launcher.Requests, request => request.Arguments.Contains("add"));
        Assert.Equal(original, add.Arguments[^1]);
        Assert.DoesNotContain("selected-base", add.Arguments);
    }
}
