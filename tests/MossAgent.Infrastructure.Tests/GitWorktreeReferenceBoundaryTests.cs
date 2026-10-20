using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeReferenceBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedReferenceCannotAuthorizeAnOutsideOrLinkedRepository(bool linked)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var project = fixture.Project with { AuthorizedDirectories = [fixture.Outside] };
        if (linked)
        {
            var alias = Path.Combine(fixture.Root, "alias");
            await fixture.JunctionAsync(alias, fixture.Repository);
            project = fixture.Project with { PrimaryDirectory = alias, AuthorizedDirectories = [alias] };
        }
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateAsync(project, id, fixture.Token, "main"));
        Assert.False(Directory.Exists(fixture.Target(id)));
        await fixture.AssertSentinelAsync();
    }

    [Fact]
    public async Task MultipleRootsStillUseOnlyTheSelectedPrimaryRepository()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var head = await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD");
        var project = fixture.Project with { AuthorizedDirectories = [fixture.Outside, fixture.Repository] };
        var path = await fixture.Service.CreateAsync(project, Guid.NewGuid(), fixture.Token, "main");
        Assert.Equal(head, await fixture.GitAsync(path, "rev-parse", "HEAD"));
        await fixture.AssertSentinelAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectedReferenceResolutionRespectsCancellationAndTheOriginalTotalDeadline(bool timeout)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var clock = new ManualToolTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var metadata = Path.Combine(fixture.Repository, ".git");
        var discovery = new StubTaskProcess
        {
            StandardOutput = new StringReader($"{fixture.Repository}\n{metadata}\n{metadata}\n{new string('a', 40)}\n"),
            Wait = _ => { clock.Advance(TimeSpan.FromSeconds(20)); return Task.CompletedTask; }
        };
        var resolving = new StubTaskProcess
        { Wait = token => { entered.SetResult(); return Task.Delay(Timeout.InfiniteTimeSpan, token); } };
        var responses = new Queue<StubTaskProcess>([discovery, resolving]);
        var launcher = new RecordingTaskProcessLauncher(() => responses.Dequeue());
        var service = new GitWorktreeService(fixture.Paths, new GitProcessRunner(launcher), clock);
        var id = Guid.NewGuid();
        var execution = service.CreateAsync(fixture.Project, id, cancellation.Token, "main");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), fixture.Token);
        if (timeout) clock.Advance(TimeSpan.FromSeconds(10));
        else await cancellation.CancelAsync();
        if (timeout) await Assert.ThrowsAsync<TimeoutException>(() => execution);
        else
        {
            var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => execution);
            Assert.Equal(cancellation.Token, failure.CancellationToken);
        }
        Assert.True(discovery.Disposed);
        Assert.True(resolving.Disposed);
        Assert.Equal(2, launcher.Requests.Count);
        Assert.Contains("--end-of-options", launcher.Requests[1].Arguments);
        Assert.DoesNotContain(launcher.Requests, request => request.Arguments.Contains("add"));
        Assert.False(Directory.Exists(fixture.Target(id)));
    }
}
