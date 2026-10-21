using System.Security.Cryptography;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class BrowserArtifactFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitApprovalProducesOneHashedNativeArtifactAndReleasesStaging(bool download)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        var result = await fixture.ExecuteAsync(download);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(1, fixture.Approvals);
        var artifact = Assert.Single(result.Artifacts!);
        var bytes = await File.ReadAllBytesAsync(artifact.Path, fixture.Files.Token);
        Assert.Equal(bytes.Length, artifact.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), artifact.Sha256);
        Assert.Equal(fixture.Files.ArtifactRoot, Path.GetDirectoryName(artifact.Path));
        Assert.Equal(artifact.Path, Assert.Single(Directory.GetFiles(fixture.Files.ArtifactRoot)));
        var output = fixture.Sessions.Sessions.Last();
        Assert.True(output.IsComplete);
        Assert.True(output.IsRetained);
        Assert.All(fixture.Sessions.Sessions, session => Assert.False(session.Stream.CanRead));
        if (download)
        {
            Assert.Equal(2, fixture.Sessions.Sessions.Count);
            Assert.False(fixture.Sessions.Sessions[0].IsComplete);
            Assert.False(fixture.Sessions.Sessions[0].IsRetained);
            Assert.Equal("fixture download", System.Text.Encoding.UTF8.GetString(bytes));
            Assert.EndsWith(".txt", artifact.Path);
            Assert.Equal("report.txt", artifact.Name);
            Assert.Equal("text/plain", artifact.MediaType);
        }
        else
        {
            Assert.True(fixture.Browser.FullPage);
            Assert.Single(fixture.Sessions.Sessions);
            Assert.Equal(fixture.Browser.ScreenshotBytes, bytes);
            Assert.Equal("image/png", artifact.MediaType);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DeniedApprovalOrReadOnlyPolicyNeverCapturesDownloadsOrCreatesFiles(bool download, bool readOnly)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        var context = fixture.Context with { ApprovalPolicy = readOnly ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime };
        var result = await fixture.ExecuteAsync(download, context, approve: false);
        Assert.Equal(readOnly ? "capability_denied" : "approval_denied", result.ErrorCode);
        Assert.Equal(readOnly ? 0 : 1, fixture.Approvals);
        Assert.Equal(0, fixture.Browser.Downloads + fixture.Browser.Captures);
        Assert.Empty(fixture.Sessions.Sessions);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Files.ArtifactParent));
    }

    [Theory]
    [InlineData("../../escape.txt", "escape.txt", ".txt")]
    [InlineData("C:\\outside\\report.PDF", "report.PDF", ".pdf")]
    [InlineData("payload.weird-ext", "payload.weird-ext", ".bin")]
    [InlineData("name\r\n.txt", "name.txt", ".txt")]
    public async Task SuggestedNamesCannotChooseStorageDirectoryOrExtension(string suggested, string display, string extension)
    {
        using var fixture = new BrowserArtifactFlowFixture();
        fixture.Browser.SuggestedName = suggested;
        var result = await fixture.ExecuteAsync(true);
        Assert.True(result.IsSuccess, result.Summary);
        var artifact = Assert.Single(result.Artifacts!);
        Assert.Equal(display, artifact.Name);
        Assert.EndsWith(extension, artifact.Path);
        Assert.Equal(fixture.Files.ArtifactRoot, Path.GetDirectoryName(artifact.Path));
        Assert.Equal("handle fixture", await File.ReadAllTextAsync(fixture.Files.Source, fixture.Files.Token));
    }
}
