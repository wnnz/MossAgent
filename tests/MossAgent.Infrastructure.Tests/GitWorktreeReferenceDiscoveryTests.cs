using MossAgent.Application.Git;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class GitWorktreeReferenceDiscoveryTests
{
    [Fact]
    public async Task RealRepositoryListsLocalBranchesAndTagsPinnedToCommits()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var head = await fixture.GitAsync(fixture.Repository, "rev-parse", "HEAD");
        await fixture.GitAsync(fixture.Repository, "branch", "feature/list");
        await fixture.GitAsync(fixture.Repository, "tag", "v1.0");
        await fixture.GitAsync(fixture.Repository, "tag", "-a", "v2.0", "-m", "annotated tag");

        var page = await Discovery().SearchAsync(fixture.Project, null, 0, 50, fixture.Token);

        Assert.False(page.Truncated);
        var branch = Assert.Single(page.Items, item => item.Name == "feature/list");
        Assert.Equal(GitWorktreeReferenceKind.Branch, branch.Kind);
        Assert.Equal("refs/heads/feature/list", branch.RefName);
        Assert.Equal(head, branch.Commit);
        Assert.Equal("initial", branch.Subject);
        var lightweight = Assert.Single(page.Items, item => item.Name == "v1.0");
        var annotated = Assert.Single(page.Items, item => item.Name == "v2.0");
        Assert.Equal(GitWorktreeReferenceKind.Tag, lightweight.Kind);
        Assert.Equal(GitWorktreeReferenceKind.Tag, annotated.Kind);
        Assert.Equal("refs/tags/v1.0", lightweight.RefName);
        Assert.Equal("refs/tags/v2.0", annotated.RefName);
        Assert.Equal(head, lightweight.Commit);
        Assert.Equal(head, annotated.Commit);
        Assert.Equal("initial", annotated.Subject);
        Assert.Contains(page.Items, item => item.Name == "main");
    }

    [Fact]
    public async Task SearchFiltersNamesCaseInsensitivelyAndPages()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        await fixture.GitAsync(fixture.Repository, "branch", "Feature/Alpha");
        await fixture.GitAsync(fixture.Repository, "branch", "feature/beta");
        var discovery = Discovery();

        var first = await discovery.SearchAsync(fixture.Project, "FEATURE", 0, 1, fixture.Token);
        Assert.Single(first.Items);
        Assert.True(first.HasMore);
        var second = await discovery.SearchAsync(fixture.Project, "feature", 1, 1, fixture.Token);
        Assert.Single(second.Items);
        Assert.Equal(2, second.NextSkip);
        Assert.False(second.HasMore);
        Assert.NotEqual(first.Items[0].RefName, second.Items[0].RefName);
        var none = await discovery.SearchAsync(fixture.Project, "missing", 0, 50, fixture.Token);
        Assert.Empty(none.Items);
        Assert.False(none.HasMore);
    }

    [Fact]
    public async Task NonCommitReferencesAreSkipped()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        await fixture.GitAsync(fixture.Repository, "tag", "tree-tag", "HEAD^{tree}");

        var page = await Discovery().SearchAsync(fixture.Project, null, 0, 50, fixture.Token);

        Assert.DoesNotContain(page.Items, item => item.Name == "tree-tag");
        Assert.Contains(page.Items, item => item.Name == "main");
    }

    [Fact]
    public async Task UnauthorizedRepositoryIsRejectedBeforeAnyGitInvocation()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));
        var project = fixture.Project with { AuthorizedDirectories = [fixture.Outside] };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => discovery.SearchAsync(project, null, 0, 10, fixture.Token));
        Assert.Empty(launcher.Requests);
    }

    [Fact]
    public async Task FailedListingSurfacesAsFailure()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var launcher = Launcher(fixture.Repository,
            new StubTaskProcess { ExitCode = 128, StandardError = new StringReader("denied") });
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => discovery.SearchAsync(fixture.Project, null, 0, 10, fixture.Token));

        var request = Assert.Single(launcher.Requests, item => item.Arguments.Contains("for-each-ref"));
        Assert.Contains("refs/heads/", request.Arguments);
        Assert.Contains("refs/tags/", request.Arguments);
        Assert.DoesNotContain("fetch", request.Arguments);
        Assert.DoesNotContain("ls-remote", request.Arguments);
    }

    [Fact]
    public async Task MalformedListingIsRejected()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var launcher = Launcher(fixture.Repository,
            new StubTaskProcess { StandardOutput = new StringReader("refs/heads/main\tshort\n") });
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => discovery.SearchAsync(fixture.Project, null, 0, 10, fixture.Token));
    }

    [Fact]
    public async Task TruncatedListingReportsTruncationAndKeepsCompleteRecords()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var record = $"refs/heads/branch\t{new string('a', 40)}\tcommit\t\t\t\tlong subject for padding\n";
        var output = string.Concat(Enumerable.Repeat(record, 400));
        var launcher = Launcher(fixture.Repository,
            new StubTaskProcess { StandardOutput = new StringReader(output) });
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));

        var page = await discovery.SearchAsync(fixture.Project, null, 0, 50, fixture.Token);

        Assert.True(page.Truncated);
        Assert.Equal(50, page.Items.Count);
        Assert.True(page.HasMore);
        Assert.All(page.Items, item => Assert.Equal("branch", item.Name));
    }

    [Fact]
    public async Task CancellationBeforeStartNeverLaunchesGit()
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => discovery.SearchAsync(fixture.Project, null, 0, 10, cancellation.Token));
        Assert.Empty(launcher.Requests);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 51)]
    public async Task PagingBoundsAreEnforced(int skip, int take)
    {
        using var fixture = await GitWorktreeSecurityFixture.CreateAsync();
        var launcher = new RecordingTaskProcessLauncher(() => new StubTaskProcess());
        var discovery = new GitWorktreeReferenceDiscovery(new GitProcessRunner(launcher));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => discovery.SearchAsync(fixture.Project, null, skip, take, fixture.Token));
        Assert.Empty(launcher.Requests);
    }

    private static GitWorktreeReferenceDiscovery Discovery() =>
        new(new GitProcessRunner(new FixtureTaskProcessLauncher()));

    private static RecordingTaskProcessLauncher Launcher(string repository, params StubTaskProcess[] afterIdentity)
    {
        var metadata = Path.Combine(repository, ".git");
        var identity = new StubTaskProcess { StandardOutput = new StringReader($"{repository}\n{metadata}\n{metadata}\n") };
        var queue = new Queue<StubTaskProcess>([identity, .. afterIdentity]);
        return new RecordingTaskProcessLauncher(queue.Dequeue);
    }
}
