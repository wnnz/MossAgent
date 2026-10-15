using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeReviewTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16")]
    [InlineData("utf16-be")]
    [InlineData("utf32")]
    [InlineData("utf32-be")]
    public async Task Review_UsesVerifiedHistoricalTextNotTheLiveFile(string format)
    {
        using var fixture = new FileChangeReviewFixture();
        var encoding = format switch
        {
            "utf8-bom" => new UTF8Encoding(true),
            "utf16" => Encoding.Unicode,
            "utf16-be" => Encoding.BigEndianUnicode,
            "utf32" => Encoding.UTF32,
            "utf32-be" => new UTF32Encoding(true, true),
            _ => new UTF8Encoding(false)
        };
        var change = await fixture.ChangeAsync(encoding);
        await File.WriteAllTextAsync(fixture.Path, "external sentinel", fixture.Token);
        var result = await fixture.ReviewAsync(change, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.True(result.IsSuccess, result.Summary);
        var artifact = Assert.Single(result.Artifacts!);
        var patch = await File.ReadAllTextAsync(artifact.Path, fixture.Token);
        Assert.Contains("-原文", patch);
        Assert.Contains("+更新", patch);
        Assert.Contains(" 保留", patch);
        Assert.DoesNotContain("external sentinel", patch);
        Assert.DoesNotContain("before.txt", patch);
        Assert.Equal("text/x-diff", artifact.MediaType);
        Assert.Equal(artifact.Sha256, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(artifact.Path, fixture.Token))));
        Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        Assert.Null(result.FileChanges);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Fact]
    public async Task NewFile_UsesAnEmptyBeforeVersionWithoutRequiringGitRepository()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(create: true);
        File.Delete(fixture.Path);
        var result = await fixture.ReviewAsync(change);
        Assert.True(result.IsSuccess, result.Summary);
        var patch = await File.ReadAllTextAsync(Assert.Single(result.Artifacts!).Path, fixture.Token);
        Assert.Contains("@@ -0,0 +1,2 @@", patch);
        Assert.Contains("+更新", patch);
        Assert.False(Directory.Exists(System.IO.Path.Combine(fixture.Root, ".git")));
        Assert.False(File.Exists(fixture.Path));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("length")]
    [InlineData("other-task")]
    public async Task InvalidSnapshot_CannotFallBackToTheCurrentFile(string damage)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var context = fixture.Context;
        var path = System.IO.Path.Combine(context.ArtifactDirectory!, change.BeforeSnapshot!.FileName);
        if (damage == "missing") File.Delete(path);
        else if (damage == "hash") await File.WriteAllTextAsync(path, "篡改\n保留\n", new UTF8Encoding(false), fixture.Token);
        else if (damage == "length") change = change with { BeforeSnapshot = change.BeforeSnapshot with { Length = 1 } };
        else context = context with { TaskId = Guid.NewGuid(), ArtifactDirectory = System.IO.Path.Combine(fixture.Root, "other-task") };
        var result = await fixture.ReviewAsync(change, context);
        Assert.Equal("snapshot_invalid", result.ErrorCode);
        Assert.Null(result.Artifacts);
        Assert.Equal("更新\n保留\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Theory]
    [InlineData("legacy", "snapshot_unavailable")]
    [InlineData("outside", "path_denied")]
    [InlineData("traversal", "invalid_arguments")]
    [InlineData("mismatch", "invalid_arguments")]
    [InlineData("denied", "approval_denied")]
    [InlineData("timeout", "tool_timeout")]
    [InlineData("output", "snapshot_review_too_large")]
    public async Task PipelineBoundaries_RejectWithoutArtifacts(string scenario, string error)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var context = fixture.Context;
        if (scenario == "legacy") change = change with { BeforeSnapshot = null, AfterSnapshot = null };
        else if (scenario == "outside") change = change with { Path = System.IO.Path.Combine(fixture.Root, "..", "outside.txt") };
        else if (scenario == "traversal") change = change with { AfterSnapshot = change.AfterSnapshot! with { FileName = "../escape.snapshot" } };
        else if (scenario == "mismatch") change = change with { AfterSha256 = new string('A', 64) };
        else if (scenario == "denied") context = context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        else if (scenario == "timeout") context = context with { Timeout = TimeSpan.Zero };
        else context = context with { MaximumOutputCharacters = 128 };
        var result = await fixture.ReviewAsync(change, context, approve: scenario != "denied");
        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.Artifacts);
        Assert.Empty(Directory.GetFiles(fixture.Context.ArtifactDirectory!, "*.diff"));
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Fact]
    public async Task PreCancellation_NeverCreatesAPatch()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancelled.Cancel();
        var result = await fixture.ReviewAsync(change, token: cancelled.Token);
        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.Empty(Directory.GetFiles(fixture.Context.ArtifactDirectory!, "*.diff"));
    }
}
