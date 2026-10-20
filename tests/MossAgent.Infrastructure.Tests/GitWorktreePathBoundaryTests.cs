using MossAgent.Infrastructure.Git;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreePathBoundaryTests
{
    [Fact]
    public async Task CreatesCleanWorktreeWithoutOtherApplicationFoldersAndRetainsBranchOnRemoval()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var taskId = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, taskId, fixture.Token);
        Assert.Equal(fixture.Target(taskId), target);
        Assert.False(Directory.Exists(fixture.Paths.ArtifactsPath));
        Assert.False(Directory.Exists(fixture.Paths.BrowserProfilesPath));
        Assert.Equal(await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD"), await fixture.GitAsync(target, "rev-parse", "HEAD"));
        await fixture.Service.RemoveAsync(fixture.Project, target, fixture.Token);
        Assert.False(Directory.Exists(target));
        Assert.Contains($"mossagent/{taskId:N}", await fixture.GitAsync(fixture.Repository, "branch", "--list"));
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingManagedDirectoryOrFileIsNeverOverwritten(bool file)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var taskId = Guid.NewGuid();
        var target = fixture.Target(taskId);
        Directory.CreateDirectory(fixture.Paths.WorktreesPath);
        if (file) await File.WriteAllTextAsync(target, "retained", fixture.Token);
        else
        {
            Directory.CreateDirectory(target);
            await File.WriteAllTextAsync(Path.Combine(target, "retained.txt"), "retained", fixture.Token);
        }
        await Assert.ThrowsAsync<IOException>(() => fixture.Service.CreateAsync(fixture.Project, taskId, fixture.Token));
        Assert.Equal("retained", await File.ReadAllTextAsync(file ? target : Path.Combine(target, "retained.txt"), fixture.Token));
        Assert.Empty(await fixture.GitAsync(fixture.Repository, "branch", "--list", $"mossagent/{taskId:N}"));
    }

    [Fact]
    public async Task RemovalRejectsRootNestedSiblingTraversalAndRelativePaths()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var taskId = Guid.NewGuid();
        var target = await fixture.Service.CreateAsync(fixture.Project, taskId, fixture.Token);
        var invalid = new[] { fixture.Paths.WorktreesPath, Path.Combine(target, "nested"),
            fixture.Paths.WorktreesPath + "-sibling" + Path.DirectorySeparatorChar + taskId.ToString("N"),
            Path.Combine(fixture.Paths.WorktreesPath, "..", "outside", taskId.ToString("N")),
            Path.Combine("worktrees", taskId.ToString("N")), fixture.Outside };
        foreach (var path in invalid)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.RemoveAsync(fixture.Project, path, fixture.Token));
        Assert.True(Directory.Exists(target));
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData("stream")]
    [InlineData("device")]
    [InlineData("forward-device")]
    [InlineData("trailing-dot")]
    [InlineData("trailing-space")]
    [InlineData("reserved")]
    public async Task WindowsAliasesAreRejectedBeforeGitLaunch(string alias)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var target = fixture.Target(Guid.NewGuid());
        var path = alias switch
        {
            "stream" => target + ":hidden",
            "device" => @"\\?\" + target,
            "forward-device" => "//?/" + target.Replace('\\', '/'),
            "trailing-dot" => target + ".",
            "trailing-space" => target + " ",
            _ => Path.Combine(fixture.Paths.WorktreesPath, "CON", Guid.NewGuid().ToString("N"))
        };
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Git must not launch."));
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, path, fixture.Token));
        Assert.Empty(launcher.Requests);
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData("target")]
    [InlineData("root")]
    [InlineData("ancestor")]
    [InlineData("dangling")]
    public async Task ManagedAncestryCannotFollowJunctions(string location)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var taskId = Guid.NewGuid();
        var paths = location == "ancestor" ? new AppDataPaths(Path.Combine(fixture.Root, "linked-parent", "app-data")) : fixture.Paths;
        var target = Path.Combine(paths.WorktreesPath, taskId.ToString("N"));
        var link = location switch { "root" => paths.WorktreesPath, "ancestor" => Path.GetDirectoryName(paths.Root)!, _ => target };
        var destination = location == "dangling" ? Path.Combine(fixture.Outside, "absent") : fixture.Outside;
        if (location == "dangling") Directory.CreateDirectory(destination);
        await fixture.JunctionAsync(link, destination);
        if (location == "dangling") Directory.Delete(destination);
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Git must not launch."));
        var service = new GitWorktreeService(paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(fixture.Project, taskId, fixture.Token));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, target, fixture.Token));
        Assert.Empty(launcher.Requests);
        Assert.Equal(new[] { "sentinel.txt" }, Directory.GetFiles(fixture.Outside).Select(Path.GetFileName));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task NestedAuthorizationDoesNotGrantWholeRepositoryButExplicitAdditionalRootDoes()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var nested = Path.Combine(fixture.Repository, "nested");
        Directory.CreateDirectory(nested);
        var project = fixture.Project with { PrimaryDirectory = nested, AuthorizedDirectories = [nested] };
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Git must not launch."));
        var denied = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.CreateAsync(project, Guid.NewGuid(), fixture.Token));
        Assert.Empty(launcher.Requests);
        Assert.False(Directory.Exists(fixture.Paths.Root));
        project = project with { AuthorizedDirectories = [nested, fixture.Repository] };
        var target = await fixture.Service.CreateAsync(project, Guid.NewGuid(), fixture.Token);
        await fixture.Service.RemoveAsync(project, target, fixture.Token);
    }

    [Fact]
    public async Task ProjectRootedInLinkedWorktreeUsesItsCommitNotPrimaryRepositoryHead()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var linked = Path.Combine(fixture.Root, "linked-source");
        await fixture.GitAsync(fixture.Repository, "worktree", "add", "-b", "source", linked);
        await fixture.GitAsync(linked, "commit", "--allow-empty", "-m", "linked-only");
        var expected = await fixture.GitAsync(linked, "rev-parse", "HEAD");
        Assert.NotEqual(await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD"), expected);
        var project = fixture.Project with { PrimaryDirectory = linked, AuthorizedDirectories = [linked] };
        var target = await fixture.Service.CreateAsync(project, Guid.NewGuid(), fixture.Token);
        Assert.Equal(expected, await fixture.GitAsync(target, "rev-parse", "HEAD"));
        await fixture.Service.RemoveAsync(project, target, fixture.Token);
    }
}
