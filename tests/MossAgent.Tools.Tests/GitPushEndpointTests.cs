using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitPushEndpointTests
{
    [Theory]
    [InlineData("https://user:private-value@example.invalid/repo?token=private-value", "https")]
    [InlineData("http://user:private-value@example.invalid:8080/repo", "http")]
    [InlineData("ssh://user:private-value@example.invalid/repo", "ssh")]
    [InlineData("git://example.invalid/repo?private-value", "git")]
    [InlineData("private-value@example.invalid:repo.git", "ssh")]
    [InlineData("private-value@[2001:db8::1]:repo.git", "ssh")]
    public async Task StandardNetworkTransportsDoNotAuthorizeLocalPathsOrExposeCredentials(string url, string protocol)
    {
        var endpoint = await new GitPushEndpoints(new GitPushDestinationProbe()).ReadAsync(url, Path.GetTempPath(), "main",
            new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess), TestContext.Current.CancellationToken);
        Assert.Equal(protocol, endpoint.Protocol);
        Assert.DoesNotContain("private-value", endpoint.Label);
        Assert.DoesNotContain("user", endpoint.Label);
        Assert.DoesNotContain(":-1", endpoint.Label);
        Assert.StartsWith(protocol + "://", endpoint.Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.invalid/repo\nprivate-value")]
    [InlineData("ext::private-value")]
    [InlineData("custom://example.invalid/private-value")]
    [InlineData("ftp://example.invalid/private-value")]
    [InlineData("file:///C:/repo?private-value")]
    [InlineData("file:///C:/repo#private-value")]
    [InlineData("example.invalid?token=private-value:repo")]
    public async Task UnsupportedAndAmbiguousTransportsFailWithSanitizedErrors(string url)
    {
        var error = await Assert.ThrowsAsync<GitPushException>(() => new GitPushEndpoints(new GitPushDestinationProbe()).ReadAsync(url,
            Path.GetTempPath(), "main", new(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()], ApprovalPolicy.FullAccess), TestContext.Current.CancellationToken));
        Assert.Equal("git_push_transport", error.Code);
        Assert.DoesNotContain("private-value", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("@")]
    [InlineData("-option")]
    [InlineData("a..b")]
    [InlineData("a@{b")]
    [InlineData("a b")]
    [InlineData("a\nb")]
    [InlineData("a:b")]
    [InlineData("a\\b")]
    [InlineData("a//b")]
    [InlineData("/a")]
    [InlineData("a/")]
    [InlineData("a/.hidden")]
    [InlineData("a/b.")]
    [InlineData("a/b.lock")]
    public void InvalidRefNamesCannotBecomePushTargetsOrConfigurationKeys(string value)
    {
        Assert.False(GitPushNames.IsBranch(value));
        Assert.False(GitPushNames.IsRemote(value));
    }

    [Theory]
    [InlineData("origin.mirror=false")]
    [InlineData("origin\".mirror")]
    public void RemoteNamesCannotInjectDynamicConfiguration(string value) => Assert.False(GitPushNames.IsRemote(value));
}
