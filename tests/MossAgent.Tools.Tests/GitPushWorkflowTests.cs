using MossAgent.Domain;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitPushWorkflowTests
{
    [Fact]
    public async Task ConfirmedPushSendsOnlyTheCapturedCommitAndOneBranchDespiteBroadDefaults()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        await fixture.SourceGitAsync("config", "remote.origin.mirror", "true");
        await fixture.SourceGitAsync("config", "push.followTags", "true");
        await fixture.SourceGitAsync("config", "push.recurseSubmodules", "on-demand");
        await fixture.SourceGitAsync("tag", "-a", "not-pushed", "-m", "private tag");
        await fixture.SourceGitAsync("branch", "unselected");
        await File.WriteAllTextAsync(Path.Combine(fixture.Source, "tracked.txt"), "not committed", TestContext.Current.CancellationToken);
        var preview = await fixture.PreviewAsync();
        var result = await fixture.PushAsync(preview);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Null(result.Content);
        Assert.Equal(preview.Head, (await fixture.RemoteGitAsync("rev-parse", "refs/heads/" + preview.TargetBranch)).Trim());
        Assert.Equal("refs/heads/" + preview.TargetBranch, (await fixture.RemoteGitAsync("for-each-ref", "--format=%(refname)")).Trim());
        Assert.Equal("baseline\n", await fixture.RemoteGitAsync("show", "refs/heads/" + preview.TargetBranch + ":tracked.txt"));
        Assert.Equal("not committed", await File.ReadAllTextAsync(Path.Combine(fixture.Source, "tracked.txt"), TestContext.Current.CancellationToken));
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("branch")]
    [InlineData("url")]
    [InlineData("config")]
    public async Task ApprovalTimeSourceOrDestinationChangesRequireAnotherPreview(string change)
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var preview = await fixture.PreviewAsync();
        fixture.OnApproval = async () =>
        {
            if (change == "head") await fixture.SourceGitAsync("commit", "--allow-empty", "-m", "new unreviewed commit");
            if (change == "branch") await fixture.SourceGitAsync("switch", "-c", "new-branch");
            if (change == "url") await fixture.SourceGitAsync("remote", "set-url", "--push", "origin", fixture.OtherRemote);
            if (change == "config") await fixture.SourceGitAsync("config", "push.followTags", "true");
        };
        var result = await fixture.PushAsync(preview);
        Assert.Equal("git_state_changed", result.ErrorCode);
        Assert.Empty(await fixture.RemoteGitAsync("for-each-ref"));
    }

    [Fact]
    public async Task ReadOnlyApprovalDenialAndUnauthorizedFileDestinationDoNotPush()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var preview = await fixture.PreviewAsync();
        Assert.Equal("capability_denied", (await fixture.PushAsync(preview, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly })).ErrorCode);
        fixture.Approved = false;
        Assert.Equal("approval_denied", (await fixture.PushAsync(preview)).ErrorCode);
        fixture.Approved = true;
        var denied = await fixture.ExecuteAsync("git.push_preview", new { }, fixture.Context with { AuthorizedRoots = [fixture.Source] });
        Assert.Equal("path_denied", denied.ErrorCode);
        Assert.Empty(await fixture.RemoteGitAsync("for-each-ref"));
    }

    [Fact]
    public async Task NonFastForwardRemoteHistoryIsPreserved()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var preview = await fixture.PreviewAsync();
        Assert.True((await fixture.PushAsync(preview)).IsSuccess);
        var divergent = (await fixture.SourceGitAsync("commit-tree", "HEAD^{tree}", "-m", "unrelated remote history")).Trim();
        await fixture.SourceGitAsync("push", fixture.Remote, divergent + ":refs/heads/setup-divergent");
        await fixture.RemoteGitAsync("update-ref", "refs/heads/" + preview.TargetBranch, divergent);
        preview = await fixture.PreviewAsync();
        Assert.Equal("git_push_failed", (await fixture.PushAsync(preview)).ErrorCode);
        Assert.Equal(divergent, (await fixture.RemoteGitAsync("rev-parse", "refs/heads/" + preview.TargetBranch)).Trim());
    }

    [Fact]
    public async Task LocalPreviewHidesUrlCredentialsAndRejectsMultiplePushUrlsAndHelpers()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        await fixture.SourceGitAsync("remote", "set-url", "--push", "origin", "https://fixture-user:private-fixture-value@example.invalid/project?token=private-fixture-value");
        var result = await fixture.ExecuteAsync("git.push_preview", new { });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.DoesNotContain("private-fixture-value", result.ToString());
        Assert.DoesNotContain("fixture-user", result.ToString());
        await fixture.SourceGitAsync("remote", "set-url", "--add", "--push", "origin", fixture.Remote);
        Assert.Equal("git_push_multiple_urls", (await fixture.ExecuteAsync("git.push_preview", new { })).ErrorCode);
        await fixture.SourceGitAsync("config", "--unset-all", "remote.origin.pushurl");
        await fixture.SourceGitAsync("config", "remote.origin.vcs", "untrusted-helper");
        Assert.Equal("git_push_transport", (await fixture.ExecuteAsync("git.push_preview", new { })).ErrorCode);
        Assert.Empty(await fixture.RemoteGitAsync("for-each-ref"));
    }
}
