using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Application.Git;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceGitReferencesViewModelTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task BranchesAndTagsAreGroupedSearchedAndSelectedAsFixedCommits() =>
        _session.DispatchAsync(async () =>
        {
            var commit = new string('b', 40);
            var probe = new GitReferenceDiscoveryProbe
            {
                Items =
                [
                    new("main", "refs/heads/main", GitReferenceKind.Branch, commit),
                    new("feature/ui", "refs/heads/feature/ui", GitReferenceKind.Branch, commit),
                    new("v1.0", "refs/tags/v1.0", GitReferenceKind.Tag, commit)
                ],
                Preview = new("", commit, "fixture subject", "Fixture Author", DateTimeOffset.UnixEpoch)
            };
            var selected = string.Empty;
            using var subject = new WorkspaceGitReferencesViewModel(probe, value => selected = value);
            subject.SetProject(new(Guid.NewGuid(), "refs", Path.GetTempPath(), [Path.GetTempPath()], DateTimeOffset.UtcNow),
                true);
            subject.Open();
            await WaitUntilAsync(() => subject.HasResults && !subject.IsBusy);
            Assert.Equal(2, subject.Branches.Count);
            Assert.Single(subject.Tags);

            subject.Search = "v1";
            await subject.SearchCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => !subject.IsBusy && subject.Tags.Count == 1 && !subject.HasBranches);
            Assert.Equal("v1", probe.LastQuery);

            subject.Search = string.Empty;
            await subject.SearchCommand.ExecuteAsync(null);
            await WaitUntilAsync(() => !subject.IsBusy && subject.Branches.Count == 2);
            await subject.Branches[0].SelectCommand.ExecuteAsync(null);
            Assert.Equal(commit, selected);
            Assert.True(subject.HasPreview);
            Assert.Contains("fixture subject", subject.Preview);
        }, TestContext.Current.CancellationToken);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(15, timeout.Token);
    }
}
