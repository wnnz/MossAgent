using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourcePullRequestLinkTests
{
    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/files")]
    [InlineData("/changes/")]
    [InlineData("/commits")]
    [InlineData("/checks")]
    [InlineData("?diff=split&w=1#discussion_r42")]
    [InlineData("/files?short_path=abc#diff-%E6%96%87%E4%BB%B6")]
    [InlineData("/commits/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa#diff-comment")]
    [InlineData("/files/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")]
    public void CommonLinksIdentifyOnlyTheCurrentPr(string suffix)
    {
        var link = SourcePullRequestLink.Parse("https://GitHub.com/Fixture/Repo.Name_1/pull/007" + suffix);
        Assert.Equal(new Uri("https://github.com"), link.Origin);
        Assert.Equal("fixture", link.Owner); Assert.Equal("repo.name_1", link.Repository); Assert.Equal(7, link.Number);
        Assert.DoesNotContain("discussion", link.ToString(), StringComparison.Ordinal);
        Assert.True(link.MatchesEndpoint(new Uri("https://api.github.com/")));
    }

    [Theory]
    [InlineData("https://enterprise.example.test/team/repo/pull/42", "https://enterprise.example.test/api/v3/", true)]
    [InlineData("https://enterprise.example.test:8443/team/repo/pull/42", "https://enterprise.example.test:8443/api/v3/", true)]
    [InlineData("https://enterprise.example.test/team/repo/pull/42", "https://enterprise.example.test/", true)]
    [InlineData("https://github.com:8443/team/repo/pull/42", "https://api.github.com/", false)]
    [InlineData("https://api.github.com/team/repo/pull/42", "https://api.github.com/", false)]
    [InlineData("https://github.com.attacker.test/team/repo/pull/42", "https://api.github.com/", false)]
    [InlineData("https://github.com/team/repo/pull/42", "https://enterprise.example.test/api/v3/", false)]
    [InlineData("https://enterprise.example.test/team/repo/pull/42", "https://enterprise.example.test:8443/api/v3/", false)]
    public void WebAndApiOriginsMustMatchTheSelectedInstance(string value, string endpoint, bool expected) =>
        Assert.Equal(expected, SourcePullRequestLink.Parse(value).MatchesEndpoint(new Uri(endpoint)));

    [Theory]
    [InlineData("")]
    [InlineData(" https://github.com/fixture/repo/pull/7")]
    [InlineData("http://github.com/fixture/repo/pull/7")]
    [InlineData("file:///C:/fixture/repo/pull/7")]
    [InlineData("//github.com/fixture/repo/pull/7")]
    [InlineData("https://user:credential@github.com/fixture/repo/pull/7")]
    [InlineData("https://github.com./fixture/repo/pull/7")]
    [InlineData("https://github.com/fixture/repo/pull/0")]
    [InlineData("https://github.com/fixture/repo/pull/-7")]
    [InlineData("https://github.com/fixture/repo/pull/+7")]
    [InlineData("https://github.com/fixture/repo/pull/2147483648")]
    [InlineData("https://github.com/fixture/repo/pull/７")]
    [InlineData("https://github.com/fixture/repo/issues/7")]
    [InlineData("https://github.com/fixture/repo/pulls/7")]
    [InlineData("https://github.com/fixture/repo/pull/7/unknown")]
    [InlineData("https://github.com/fixture/repo/pull/7//")]
    [InlineData("https://github.com/fixture/repo/pull/7/checks/42")]
    [InlineData("https://github.com/fixture/repo/pull/7/commits/not-a-sha")]
    [InlineData("https://github.com/fixture/repo/pull/7/files/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaz")]
    [InlineData("https://github.com/fixture/repo/pull/7/files/extra/path")]
    [InlineData("https://github.com/fixture//repo/pull/7")]
    [InlineData("https://github.com/fixture/repo/../repo/pull/7")]
    [InlineData("https://github.com/fixture/repo/./pull/7")]
    [InlineData("https://github.com/fixture/repo/%2e%2e/repo/pull/7")]
    [InlineData("https://github.com/fixture%2frepo/other/pull/7")]
    [InlineData("https://%67ithub.com/fixture/repo/pull/7")]
    [InlineData("https://github.com/fixture/repo\\pull/7")]
    [InlineData("https://github.com/fixture/repo/pull/7?secret=fixture\r\nHeader:value")]
    [InlineData("https://github.com/fixture/repo/pull/7#with space")]
    public void AmbiguousOrUnsupportedLinksAreRejectedWithoutEcho(string value)
    {
        var exception = Assert.Throws<ArgumentException>(() => SourcePullRequestLink.Parse(value));
        if (value.Length > 0) Assert.DoesNotContain(value, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("credential", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedLinksAreRejectedBeforeUriParsing() =>
        Assert.Throws<ArgumentException>(() => SourcePullRequestLink.Parse("https://github.com/fixture/repo/pull/7?" + new string('x', 4096)));
}
