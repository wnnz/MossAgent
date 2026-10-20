using System.Text;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitPushBoundaryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ObjectDirectoryJunctionBeforePreviewOrDuringApprovalCannotEscape(bool duringApproval)
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var outside = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(fixture.Remote)!, "outside")).FullName;
        var sentinel = Path.Combine(outside, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "untouched", TestContext.Current.CancellationToken);
        var link = Path.Combine(fixture.Remote, "objects", "aa");
        if (duringApproval)
        {
            var preview = await fixture.PreviewAsync();
            fixture.OnApproval = () => fixture.JunctionAsync(link, outside);
            Assert.Equal("path_denied", (await fixture.PushAsync(preview)).ErrorCode);
        }
        else
        {
            await fixture.JunctionAsync(link, outside);
            var result = await fixture.ExecuteAsync("git.push_preview", new { });
            Assert.Equal("path_denied", result.ErrorCode);
        }
        Assert.Equal("untouched", await File.ReadAllTextAsync(sentinel, TestContext.Current.CancellationToken));
        Assert.Single(Directory.EnumerateFiles(outside));
    }

    [Fact]
    public async Task RejectingPrePushHookIsNotBypassedAndItsSecretOutputIsSuppressed()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var hooks = Directory.CreateDirectory(Path.Combine(fixture.Source, "test-push-hooks")).FullName;
        await File.WriteAllTextAsync(Path.Combine(hooks, "pre-push"), "#!/bin/sh\necho private-hook-value >&2\nexit 1\n", new UTF8Encoding(false), TestContext.Current.CancellationToken);
        await fixture.SourceGitAsync("config", "core.hooksPath", hooks);
        var result = await fixture.PushAsync(await fixture.PreviewAsync());
        Assert.Equal("git_push_failed", result.ErrorCode);
        Assert.DoesNotContain("private-hook-value", result.ToString());
        Assert.Empty(await fixture.RemoteGitAsync("for-each-ref"));
        Assert.False(fixture.Audit.Completion?.IsSuccess);
    }

    [Fact]
    public async Task DetachedHeadCanPushAnExplicitTargetAndFileUriKeepsAuthorization()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        await fixture.SourceGitAsync("switch", "--detach");
        await fixture.SourceGitAsync("remote", "set-url", "--push", "origin", new Uri(fixture.Remote).AbsoluteUri);
        Assert.Equal("git_push_branch", (await fixture.ExecuteAsync("git.push_preview", new { })).ErrorCode);
        var preview = await fixture.PreviewAsync(new { remote = "origin", targetBranch = "review/detached" });
        Assert.Null(preview.Branch);
        Assert.True((await fixture.PushAsync(preview)).IsSuccess);
        Assert.Equal(preview.Head, (await fixture.RemoteGitAsync("rev-parse", "refs/heads/review/detached")).Trim());
    }

    [Fact]
    public async Task TrackingAndPushRemoteDefaultsSelectTheConfiguredTargetWithoutChangingTracking()
    {
        using var fixture = new GitPushFixture();
        await fixture.InitializeAsync();
        var branch = (await fixture.SourceGitAsync("symbolic-ref", "--short", "HEAD")).Trim();
        await fixture.SourceGitAsync("config", $"branch.{branch}.remote", "origin");
        await fixture.SourceGitAsync("config", $"branch.{branch}.merge", "refs/heads/tracked-target");
        await fixture.SourceGitAsync("config", "push.autoSetupRemote", "true");
        var preview = await fixture.PreviewAsync();
        Assert.Equal("tracked-target", preview.TargetBranch);
        Assert.True((await fixture.PushAsync(preview)).IsSuccess);
        Assert.Equal("refs/heads/tracked-target", (await fixture.SourceGitAsync("config", "--get", $"branch.{branch}.merge")).Trim());
        await fixture.SourceGitAsync("remote", "add", "upstream", fixture.OtherRemote);
        await fixture.SourceGitAsync("config", $"branch.{branch}.pushRemote", "upstream");
        preview = await fixture.PreviewAsync();
        Assert.Equal("upstream", preview.Remote);
        Assert.Equal(branch, preview.TargetBranch);
    }
}
