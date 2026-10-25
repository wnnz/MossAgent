using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitRemoteEndpointsTests
{
    [Theory]
    [InlineData("https://example.invalid/repo.git", GitRemoteCredentialMethod.Https, "https://example.invalid")]
    [InlineData("http://example.invalid:8443/repo.git", GitRemoteCredentialMethod.Https, "http://example.invalid:8443")]
    [InlineData("https://user:private-value@example.invalid/repo.git", GitRemoteCredentialMethod.Embedded, "https://example.invalid")]
    [InlineData("https://user@example.invalid/repo.git", GitRemoteCredentialMethod.Embedded, "https://example.invalid")]
    [InlineData("http://:private-value@example.invalid/repo.git", GitRemoteCredentialMethod.Embedded, "http://example.invalid")]
    [InlineData("ssh://git@example.invalid/repo.git", GitRemoteCredentialMethod.Ssh, "ssh://example.invalid")]
    [InlineData("ssh://git:private-value@example.invalid/repo.git", GitRemoteCredentialMethod.Embedded, "ssh://example.invalid")]
    [InlineData("git://example.invalid/repo.git", GitRemoteCredentialMethod.Ssh, "git://example.invalid")]
    [InlineData("git@example.invalid:repo.git", GitRemoteCredentialMethod.Ssh, "ssh://example.invalid")]
    [InlineData("example.invalid:repo.git", GitRemoteCredentialMethod.Ssh, "ssh://example.invalid")]
    [InlineData("private-value@[2001:db8::1]:repo.git", GitRemoteCredentialMethod.Ssh, "ssh://[2001:db8::1]")]
    [InlineData("ext::custom-transport", GitRemoteCredentialMethod.Unsupported, null)]
    [InlineData("custom://example.invalid/repo", GitRemoteCredentialMethod.Unsupported, null)]
    [InlineData("", GitRemoteCredentialMethod.Unsupported, null)]
    [InlineData(null, GitRemoteCredentialMethod.Unsupported, null)]
    [InlineData("https://example.invalid/repo\nprivate-value", GitRemoteCredentialMethod.Unsupported, null)]
    public void UrlShapesClassifyWithoutEchoingUserinfo(string? url, GitRemoteCredentialMethod method, string? endpoint)
    {
        var transport = GitRemoteEndpoints.Classify(url);
        Assert.Equal(method, transport.Method);
        Assert.Equal(endpoint, transport.Endpoint);
        if (transport.Endpoint is not null) Assert.DoesNotContain("private-value", transport.Endpoint);
    }

    [Theory]
    [InlineData("repo.git")]
    [InlineData("../repo.git")]
    [InlineData("nested/repo.git")]
    public void RelativePathsReadAsLocal(string url) =>
        Assert.Equal(GitRemoteCredentialMethod.Local, GitRemoteEndpoints.Classify(url).Method);

    [Fact]
    public void RootedPathsAndFileUrisReadAsLocalWithoutEchoingThePath()
    {
        var rooted = Path.GetFullPath("fixture-secret-dir/repo.git");
        var transport = GitRemoteEndpoints.Classify(rooted);
        Assert.Equal(GitRemoteCredentialMethod.Local, transport.Method);
        Assert.Null(transport.Endpoint);
        Assert.Equal(GitRemoteCredentialMethod.Local, GitRemoteEndpoints.Classify(new Uri(rooted).AbsoluteUri).Method);
        Assert.Equal(GitRemoteCredentialMethod.Unsupported, GitRemoteEndpoints.Classify(new Uri(rooted).AbsoluteUri + "?private-value").Method);
    }
}
