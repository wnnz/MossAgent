using System.Text;
using MossAgent.Testing;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeReviewSafetyTests
{
    [Fact]
    public async Task UntrustedOrSubstitutedReferences_AreDeniedAndTheToolIsNotAdvertisedToModels()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var untrusted = await fixture.ReviewAsync(change, trusted: false);
        Assert.Equal("path_denied", untrusted.ErrorCode);
        var replacement = change with { Path = Path.Combine(fixture.Root, "another.txt") };
        var substituted = await fixture.ReviewAsync(replacement, fixture.Context with { ReviewedChange = change }, trusted: false);
        Assert.Equal("path_denied", substituted.ErrorCode);
        Assert.Null(untrusted.Artifacts);
        Assert.Null(substituted.Artifacts);
        var registry = new ToolRegistry([fixture.Tool]);
        Assert.Empty(registry.Descriptors);
        Assert.True(registry.TryGet(fixture.Tool.Descriptor.Name, out var resolved));
        Assert.Same(fixture.Tool, resolved);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Theory]
    [InlineData("FF")]
    [InlineData("0041")]
    [InlineData("FFFE41")]
    [InlineData("FFFE00D8")]
    [InlineData("FFFE00004100")]
    public async Task BinaryOrMalformedText_IsRejectedBeforePatchCreation(string hex)
    {
        using var fixture = new FileChangeReviewFixture();
        await File.WriteAllBytesAsync(fixture.Path, Convert.FromHexString(hex), fixture.Token);
        var change = await fixture.ChangeAsync(create: true);
        var result = await fixture.ReviewAsync(change);
        Assert.Equal("snapshot_review_unsupported", result.ErrorCode);
        Assert.Null(result.Artifacts);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Fact]
    public async Task ArtifactDirectoryJunction_IsRejectedBeforeReadingOrCreatingFiles()
    {
        using var fixture = new FileChangeReviewFixture();
        using var outside = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var link = Path.Combine(fixture.Root, "linked-artifacts");
        await TestDirectoryJunction.CreateAsync(link, outside.Root, fixture.Token);
        try
        {
            var result = await fixture.ReviewAsync(change, fixture.Context with { ArtifactDirectory = link });
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Null(result.Artifacts);
            Assert.Empty(Directory.GetFileSystemEntries(outside.Root));
        }
        finally { Directory.Delete(link); }
    }

    [Fact]
    public async Task HeaderLikeContentAndMissingFinalNewline_AreNotRewrittenAsMetadata()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(before: "-- old content", after: "++ new content");
        var result = await fixture.ReviewAsync(change);
        Assert.True(result.IsSuccess, result.Summary);
        var patch = await File.ReadAllTextAsync(Assert.Single(result.Artifacts!).Path, fixture.Token);
        Assert.Contains("--- old content", patch);
        Assert.Contains("+++ new content", patch);
        Assert.Contains("\\ No newline at end of file", patch);
        Assert.Contains("--- \"a/edit.txt\"", patch);
        Assert.Contains("+++ \"b/edit.txt\"", patch);
    }

    [Fact]
    public async Task CarriageReturnsInHunks_ArePreservedInTheStoredPatch()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(before: "keep\r\nold\r\n", after: "keep\r\nnew\r\n");
        var result = await fixture.ReviewAsync(change);
        Assert.True(result.IsSuccess, result.Summary);
        var patch = await File.ReadAllTextAsync(Assert.Single(result.Artifacts!).Path, fixture.Token);
        Assert.Contains("-old\r\n", patch);
        Assert.Contains("+new\r\n", patch);
    }

    [Fact]
    public async Task EncodingOnlyChange_ReturnsAnEmptyPatchWithoutInventingChangedLines()
    {
        using var fixture = new FileChangeReviewFixture();
        await File.WriteAllTextAsync(fixture.Path, "same\n", Encoding.Unicode, fixture.Token);
        var change = await fixture.ChangeAsync(create: true, after: "same\n");
        Assert.NotEqual(change.BeforeSha256, change.AfterSha256);
        var result = await fixture.ReviewAsync(change);
        Assert.True(result.IsSuccess, result.Summary);
        var artifact = Assert.Single(result.Artifacts!);
        Assert.Equal(0, artifact.Length);
        Assert.Equal(string.Empty, await File.ReadAllTextAsync(artifact.Path, fixture.Token));
    }
}
