using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeExternalMetadataTests
{
    [Fact]
    public async Task StandaloneGitPointerCannotBorrowUnauthorizedExternalRepositoryMetadata()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var metadata = Path.Combine(fixture.Outside, "metadata");
        Directory.Move(Path.Combine(fixture.Repository, ".git"), metadata);
        await File.WriteAllTextAsync(Path.Combine(fixture.Repository, ".git"), $"gitdir: {metadata}\n", fixture.Token);
        var taskId = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateAsync(fixture.Project, taskId, fixture.Token));
        Assert.False(Directory.Exists(fixture.Paths.Root));
        Assert.Empty(await fixture.GitAsync(fixture.Repository, "branch", "--list", $"mossagent/{taskId:N}"));
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnauthorizedStandaloneMetadataIsRejectedBeforeNativeDiscovery(bool removal)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var metadata = Path.Combine(fixture.Outside, "metadata");
        Directory.Move(Path.Combine(fixture.Repository, ".git"), metadata);
        await File.WriteAllTextAsync(Path.Combine(fixture.Repository, ".git"), $"gitdir: {metadata}\n", fixture.Token);
        var authorized = fixture.Project with { AuthorizedDirectories = [fixture.Repository, metadata] };
        var target = removal ? await fixture.Service.CreateAsync(authorized, Guid.NewGuid(), fixture.Token) : null;
        var launcher = new RecordingTaskProcessLauncher(() => throw new InvalidOperationException("Native discovery must not launch."));
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        if (removal)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, target!, fixture.Token));
            Assert.True(Directory.Exists(target));
        }
        else await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token));
        Assert.Empty(launcher.Requests);
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task ExplicitAdditionalMetadataRootRetainsSeparateGitDirectorySupport()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var metadata = Path.Combine(fixture.Outside, "metadata");
        Directory.Move(Path.Combine(fixture.Repository, ".git"), metadata);
        await File.WriteAllTextAsync(Path.Combine(fixture.Repository, ".git"), $"gitdir: {metadata}\n", fixture.Token);
        var project = fixture.Project with { AuthorizedDirectories = [fixture.Repository, metadata] };
        var target = await fixture.Service.CreateAsync(project, Guid.NewGuid(), fixture.Token);
        Assert.Equal(await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD"), await fixture.GitAsync(target, "rev-parse", "HEAD"));
        await fixture.Service.RemoveAsync(project, target, fixture.Token);
        Assert.False(Directory.Exists(target));
        await fixture.AssertSentinelAsync();
    }
}
