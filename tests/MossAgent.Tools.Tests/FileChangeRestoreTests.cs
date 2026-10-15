using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16")]
    [InlineData("utf16-be")]
    [InlineData("utf32")]
    [InlineData("utf32-be")]
    [InlineData("binary")]
    [InlineData("empty")]
    public async Task Restore_PreservesOriginalBytesAndReportsTheInverseChange(string format)
    {
        using var fixture = new FileChangeRestoreFixture();
        var encoding = format switch
        {
            "utf8-bom" => new UTF8Encoding(true),
            "utf16" => Encoding.Unicode,
            "utf16-be" => Encoding.BigEndianUnicode,
            "utf32" => Encoding.UTF32,
            "utf32-be" => new UTF32Encoding(true, true),
            _ => new UTF8Encoding(false)
        };
        var bytes = format == "binary" ? new byte[] { 0, 0xff, 0xfe, 0, 0xd8, 42 }
            : format == "empty" ? [] : encoding.GetPreamble().Concat(encoding.GetBytes("原文😀\r\n尾行")).ToArray();
        var change = await fixture.ChangeAsync(bytes);
        var snapshotCount = Directory.GetFiles(fixture.Context.ArtifactDirectory!).Length;

        var result = await fixture.ExecuteAsync(change);

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fixture.Path, fixture.Token));
        var inverse = Assert.Single(result.FileChanges!);
        Assert.True(inverse.IsValid);
        Assert.Equal(change.AfterSha256, inverse.BeforeSha256);
        Assert.Equal(change.BeforeSha256, inverse.AfterSha256);
        Assert.Equal(change.AfterSnapshot, inverse.BeforeSnapshot);
        Assert.Equal(change.BeforeSnapshot, inverse.AfterSnapshot);
        Assert.Equal(snapshotCount, Directory.GetFiles(fixture.Context.ArtifactDirectory!).Length);
        Assert.Equal(inverse.AfterSha256, fixture.Context.FileSnapshots.Get(fixture.Context.TaskId,
            MossAgent.Tools.BuiltIn.Files.FileSnapshotReader.CacheKey(fixture.Path))!.Sha256);
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task RemoveCreatedFile_ReportsAbsenceAndRetainsAReviewableSnapshot()
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: true);
        var result = await fixture.ExecuteAsync(change, remove: true);

        Assert.True(result.IsSuccess, result.Summary);
        Assert.False(File.Exists(fixture.Path));
        var inverse = Assert.Single(result.FileChanges!);
        Assert.True(inverse.IsValid);
        Assert.Equal(change.AfterSha256, inverse.BeforeSha256);
        Assert.Null(inverse.AfterSha256);
        Assert.True(inverse.AfterDeleted);
        Assert.Null(inverse.AfterSnapshot);
        Assert.Equal(change.AfterSnapshot, inverse.BeforeSnapshot);
        Assert.Equal(ToolRiskLevel.HighRisk, fixture.Remove.Descriptor.RiskLevel);
        var executor = new ToolExecutor(new ToolRegistry([fixture.Review]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var review = await executor.ExecuteAsync(FileChangeRestoreFixture.Request(inverse, fixture.Review),
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly, ReviewedChange = inverse }, fixture.Token);
        Assert.True(review.IsSuccess, review.Summary);
        Assert.Contains("-agent update", await File.ReadAllTextAsync(Assert.Single(review.Artifacts!).Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task RestoreDeletedVersion_RecreatesOnlyTheExpectedAbsentFile()
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: true);
        var expected = await File.ReadAllBytesAsync(fixture.Path, fixture.Token);
        var deleted = Assert.Single((await fixture.ExecuteAsync(change, remove: true)).FileChanges!);

        var result = await fixture.ExecuteAsync(deleted);

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(expected, await File.ReadAllBytesAsync(fixture.Path, fixture.Token));
        var inverse = Assert.Single(result.FileChanges!);
        Assert.Null(inverse.BeforeSha256);
        Assert.Equal(change.AfterSha256, inverse.AfterSha256);
        Assert.Equal(change.AfterSnapshot, inverse.AfterSnapshot);
        Assert.False(inverse.AfterDeleted);
    }

    [Fact]
    public async Task SmallOutputBudget_KeepsTheCommitButSignalsMissingMetadata()
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync();
        var result = await fixture.ExecuteAsync(change, context: fixture.Context with { MaximumOutputCharacters = 128 });

        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(result.FileChangesTruncated);
        Assert.Empty(result.FileChanges!);
        Assert.InRange(result.OutputCharacterCount, 0, 128);
        Assert.Equal("original\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
    }

    [Fact]
    public void Descriptors_AreUiOnlyButRemainAvailableThroughStandardLookup()
    {
        using var fixture = new FileChangeRestoreFixture();
        var registry = new ToolRegistry([fixture.Restore, fixture.Remove]);

        Assert.Empty(registry.Descriptors);
        Assert.True(registry.TryGet(fixture.Restore.Descriptor.Name, out var restore));
        Assert.True(registry.TryGet(fixture.Remove.Descriptor.Name, out var remove));
        Assert.Same(fixture.Restore, restore);
        Assert.Same(fixture.Remove, remove);
    }

    [Fact]
    public async Task AnotherAuthorizedRoot_RetainsTheSameTaskSnapshotAndVersionBoundary()
    {
        using var fixture = new FileChangeRestoreFixture();
        using var secondary = new FileEditingFixture();
        var context = fixture.Context with { AuthorizedRoots = [fixture.Root, secondary.Root] };
        var change = await fixture.ChangeAsync(path: secondary.Path, context: context);

        var result = await fixture.ExecuteAsync(change, context: context);

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(secondary.Path, Assert.Single(result.FileChanges!).Path);
        Assert.Equal("original\r\n", await secondary.ContentsAsync());
        Assert.False(File.Exists(fixture.Path));
        fixture.AssertNoTemporaryFiles();
        secondary.AssertNoTemporaryFiles();
    }
}
