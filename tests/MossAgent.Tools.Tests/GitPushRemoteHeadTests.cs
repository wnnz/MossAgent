using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitPushRemoteHeadTests
{
    [Fact]
    public async Task PreviewSerializesRemoteHeadAsNullWhenTheTrackingRefIsMissing()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var result = await fixture.ExecuteAsync("git.push_preview", new { });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("\"remoteHead\"", result.Content);
        Assert.Null((await fixture.PreviewAsync()).RemoteHead);
    }

    [Fact]
    public async Task PreviewReadsTheExistingRemoteTrackingRefForTheSelectedTarget()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var preview = await fixture.PreviewAsync();
        var other = (await fixture.SourceGitAsync("commit-tree", "HEAD^{tree}", "-m", "unrelated remote history")).Trim();
        await fixture.SourceGitAsync("update-ref", $"refs/remotes/{preview.Remote}/{preview.TargetBranch}", other);
        var next = await fixture.PreviewAsync();
        Assert.Equal(other, next.RemoteHead);
        Assert.NotEqual(next.Head, next.RemoteHead);
    }

    [Fact]
    public async Task SuccessfulPushPointsRemoteHeadAtThePushedCommit()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var preview = await fixture.PreviewAsync();
        Assert.Null(preview.RemoteHead);
        Assert.True((await fixture.PushAsync(preview)).IsSuccess);
        var next = await fixture.PreviewAsync();
        Assert.Equal(preview.Head, next.RemoteHead);
        Assert.Equal($"refs/remotes/{preview.Remote}/{preview.TargetBranch}",
            (await fixture.SourceGitAsync("for-each-ref", "--format=%(refname)", "refs/remotes")).Trim());
    }

    [Fact]
    public async Task RemoteTrackingHeadParticipatesInTheStateHash()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var before = await fixture.PreviewAsync();
        var other = (await fixture.SourceGitAsync("commit-tree", "HEAD^{tree}", "-m", "remote moved")).Trim();
        var reference = $"refs/remotes/{before.Remote}/{before.TargetBranch}";
        await fixture.SourceGitAsync("update-ref", reference, other);
        var moved = await fixture.PreviewAsync();
        Assert.Equal(other, moved.RemoteHead);
        Assert.NotEqual(before.State, moved.State);
        await fixture.SourceGitAsync("update-ref", "-d", reference);
        var removed = await fixture.PreviewAsync();
        Assert.Null(removed.RemoteHead);
        Assert.Equal(before.State, removed.State);
        Assert.NotEqual(moved.State, removed.State);
    }
}
