using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeDiscoveryBoundaryTests
{
    [Theory]
    [InlineData("truncated")]
    [InlineData("missing")]
    [InlineData("invalid-head")]
    [InlineData("mismatch")]
    [InlineData("nonzero")]
    public async Task IncompleteOrInconsistentIdentityCannotCreateAnyDirectory(string failure)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var metadata = Path.Combine(fixture.Repository, ".git");
        var output = failure switch
        {
            "truncated" => new string('x', 20000),
            "missing" => fixture.Repository + "\n",
            "invalid-head" => $"{fixture.Repository}\n{metadata}\n{metadata}\ninvalid\n",
            "mismatch" => $"{fixture.Outside}\n{metadata}\n{metadata}\n{new string('a', 40)}\n",
            _ => $"{fixture.Repository}\n{metadata}\n{metadata}\n{new string('a', 40)}\n"
        };
        var process = new StubTaskProcess { ExitCode = failure == "nonzero" ? 128 : 0, StandardOutput = new StringReader(output) };
        var launcher = new RecordingTaskProcessLauncher(() => process);
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        if (failure == "mismatch")
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token));
        Assert.Single(launcher.Requests);
        Assert.False(Directory.Exists(fixture.Paths.Root));
        Assert.True(process.Disposed);
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData("unterminated")]
    [InlineData("missing-head")]
    [InlineData("truncated")]
    [InlineData("duplicate")]
    public async Task MalformedRegistrationCannotAuthorizeRemoval(string failure)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var target = await fixture.Service.CreateAsync(fixture.Project, Guid.NewGuid(), fixture.Token);
        var metadata = Path.Combine(fixture.Repository, ".git");
        var output = failure switch
        {
            "unterminated" => $"worktree {target}\0HEAD {new string('a', 40)}\0",
            "missing-head" => $"worktree {target}\0\0",
            "truncated" => $"worktree {target}\0HEAD {new string('a', 40)}\0\0{new string('x', 20000)}",
            _ => $"worktree {fixture.Repository}\0HEAD {new string('a', 40)}\0worktree {target}\0\0"
        };
        var responses = new Queue<StubTaskProcess>([
            new() { StandardOutput = new StringReader($"{fixture.Repository}\n{metadata}\n{metadata}\n") },
            new() { StandardOutput = new StringReader(output) }]);
        var launcher = new RecordingTaskProcessLauncher(() => responses.Dequeue());
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RemoveAsync(fixture.Project, target, fixture.Token));
        Assert.Equal(2, launcher.Requests.Count);
        Assert.DoesNotContain(launcher.Requests, request => request.Arguments.Contains("remove"));
        Assert.True(Directory.Exists(target));
        await fixture.AssertSentinelAsync();
    }
}
