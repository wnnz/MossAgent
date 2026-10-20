using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeMetadataBoundaryTests
{
    [Fact]
    public async Task ForeignRepositoryRegistrationCannotBeRemovedThroughSelectedProject()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        await fixture.InitializeAsync(fixture.Outside);
        var taskId = Guid.NewGuid();
        var foreign = fixture.Project with { PrimaryDirectory = fixture.Outside, AuthorizedDirectories = [fixture.Outside] };
        var target = await fixture.Service.CreateAsync(foreign, taskId, fixture.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RemoveAsync(fixture.Project, target, fixture.Token));
        Assert.True(Directory.Exists(target));
        Assert.Contains(target.Replace('\\', '/'), await fixture.GitAsync(fixture.Outside, "worktree", "list", "--porcelain"));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task CopiedGitPointerCannotImpersonateAnotherRegisteredWorktree()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var first = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        var second = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        var pointer = await File.ReadAllTextAsync(Path.Combine(first, ".git"), fixture.Token);
        File.SetAttributes(Path.Combine(second, ".git"), FileAttributes.Normal);
        await File.WriteAllTextAsync(Path.Combine(second, ".git"), pointer, fixture.Token);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RemoveAsync(fixture.Project, second, fixture.Token));
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("registration-root")]
    public async Task SourceMetadataLinksAreRejectedBeforeReadingGitConfiguration(string location)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var link = location == "metadata" ? Path.Combine(fixture.Repository, ".git") : Path.Combine(fixture.Repository, ".git", "worktrees");
        var destination = Path.Combine(fixture.Outside, "metadata");
        if (location == "metadata") Directory.Move(link, destination);
        else Directory.CreateDirectory(destination);
        await fixture.JunctionAsync(link, destination);
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Git must not launch."));
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token));
        Assert.Empty(launcher.Requests);
        Assert.False(Directory.Exists(fixture.Paths.Root));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task LinkedSourceCommonDirectoryCannotTraverseAJunctionBeforeDiscovery()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var linked = Path.Combine(fixture.Root, "linked-source");
        await fixture.GitAsync(fixture.Repository, "worktree", "add", "-b", "source", linked);
        var pointer = await File.ReadAllTextAsync(Path.Combine(linked, ".git"), fixture.Token);
        var metadata = pointer[8..].TrimEnd('\r', '\n');
        var commonLink = Path.Combine(fixture.Root, "linked-common");
        await fixture.JunctionAsync(commonLink, Path.Combine(fixture.Repository, ".git"));
        await File.WriteAllTextAsync(Path.Combine(metadata, "commondir"), commonLink, fixture.Token);
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Git must not launch."));
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        var project = fixture.Project with { PrimaryDirectory = linked, AuthorizedDirectories = [linked] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(project, Guid.NewGuid(), fixture.Token));
        Assert.Empty(launcher.Requests);
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData("bad pointer")]
    [InlineData("gitdir: \n")]
    [InlineData("oversized")]
    public async Task InvalidOrOversizedTargetPointerNeverRunsRemoval(string content)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var target = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        File.SetAttributes(Path.Combine(target, ".git"), FileAttributes.Normal);
        await File.WriteAllTextAsync(Path.Combine(target, ".git"), content == "oversized" ? new string('x', 5000) : content, fixture.Token);
        var launcher = new GitWorktreeObservingLauncher(new FixtureTaskProcessLauncher());
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, target, fixture.Token));
        Assert.DoesNotContain(launcher.Requests, request => request.Arguments.Contains("remove"));
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public async Task SourceRepositoryItselfCannotBeRemovedEvenInsideManagedStorage()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var repository = fixture.Target(Guid.NewGuid());
        await fixture.InitializeAsync(repository);
        var nested = Path.Combine(repository, "nested");
        Directory.CreateDirectory(nested);
        var project = fixture.Project with { PrimaryDirectory = nested, AuthorizedDirectories = [repository] };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RemoveAsync(project, repository, fixture.Token));
        Assert.True(Directory.Exists(Path.Combine(repository, ".git")));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task TargetReplacedWithJunctionDuringDiscoveryIsRetainedAndNeverRemoved()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var target = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        var saved = Path.Combine(fixture.Root, "retained-worktree");
        var launcher = new GitWorktreeObservingLauncher(new FixtureTaskProcessLauncher())
        {
            AfterExit = async request =>
            {
                if (!request.Arguments.Contains("rev-parse")) return;
                Directory.Move(target, saved);
                await fixture.JunctionAsync(target, fixture.Outside);
            }
        };
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, target, fixture.Token));
        Assert.DoesNotContain(launcher.Requests, request => request.Arguments.Contains("remove"));
        Assert.True(Directory.Exists(saved));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task SourceMetadataReplacedWithJunctionAfterDiscoveryNeverStartsCreation()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        await fixture.InitializeAsync(fixture.Outside);
        var metadata = Path.Combine(fixture.Repository, ".git");
        var retained = Path.Combine(fixture.Root, "retained-metadata");
        var taskId = Guid.NewGuid();
        var launcher = new GitWorktreeObservingLauncher(new FixtureTaskProcessLauncher())
        {
            AfterExit = async request =>
            {
                if (!request.Arguments.Contains("rev-parse")) return;
                Directory.Move(metadata, retained);
                await fixture.JunctionAsync(metadata, Path.Combine(fixture.Outside, ".git"));
            }
        };
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(fixture.Project, taskId, fixture.Token));
        Assert.Single(launcher.Requests);
        Assert.False(Directory.Exists(fixture.Paths.Root));
        Assert.True(Directory.Exists(retained));
        Assert.Empty(await fixture.GitAsync(fixture.Outside, "branch", "--list", $"mossagent/{taskId:N}"));
        await fixture.AssertSentinelAsync();
    }
}
