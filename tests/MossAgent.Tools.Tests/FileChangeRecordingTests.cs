using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRecordingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CommittedWrite_RecordsCanonicalPathAndActualHashes(bool replace, bool exists)
    {
        using var fixture = new FileEditingFixture();
        if (exists) await fixture.SeedAsync("alpha");
        var before = await fixture.SnapshotAsync();
        var result = replace
            ? await fixture.ExecuteAsync(fixture.Replace, new { path = "edit.txt", oldText = "alpha", newText = "beta" })
            : await fixture.ExecuteAsync(fixture.Write, new { path = "edit.txt", content = "beta" });
        var after = await fixture.SnapshotAsync();

        Assert.True(result.IsSuccess);
        var change = Assert.Single(result.FileChanges!);
        Assert.True(change.IsValid);
        Assert.Equal(fixture.Path, change.Path);
        Assert.Equal(before.Sha256, change.BeforeSha256);
        Assert.Equal(after.Sha256, change.AfterSha256);
        Assert.False(result.FileChangesTruncated);
        Assert.Equal("beta", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdenticalWrite_DoesNotInventAChange(bool replace)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha");
        var result = replace
            ? await fixture.ExecuteAsync(fixture.Replace, new { path = fixture.Path, oldText = "alpha", newText = "alpha" })
            : await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "alpha" });

        Assert.True(result.IsSuccess);
        Assert.Null(result.FileChanges);
        Assert.Equal("alpha", await fixture.ContentsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedWrite_HasNoCommittedChanges(bool escape)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha");
        var context = escape ? fixture.Context : fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        var result = await fixture.ExecuteAsync(fixture.Write,
            new { path = escape ? "../outside.txt" : fixture.Path, content = "beta" }, context);

        Assert.False(result.IsSuccess);
        Assert.Equal(escape ? "path_denied" : "capability_denied", result.ErrorCode);
        Assert.Null(result.FileChanges);
        Assert.Equal("alpha", await fixture.ContentsAsync());
    }
}
