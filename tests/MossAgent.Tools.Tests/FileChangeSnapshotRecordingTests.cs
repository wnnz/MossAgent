using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeSnapshotRecordingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Commit_PreservesExactBeforeAndStagedAfterBytes(bool replace, bool exists)
    {
        using var fixture = new FileEditingFixture();
        if (exists) await fixture.SeedAsync("原文 alpha\r\n", Encoding.Unicode);
        var before = exists ? await File.ReadAllBytesAsync(fixture.Path, fixture.Token) : null;
        var context = Context(fixture);
        var result = replace
            ? await fixture.ExecuteAsync(fixture.Replace, new { path = fixture.Path, oldText = "alpha", newText = "beta" }, context)
            : await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "新文本 beta\n" }, context);
        var change = Assert.Single(result.FileChanges!);
        Assert.True(result.IsSuccess);
        Assert.True(change.IsValid);
        var reader = new FileChangeSnapshotReader();
        if (exists)
        {
            Assert.NotNull(change.BeforeSnapshot);
            Assert.Equal(before, await reader.ReadAsync(change.BeforeSnapshot, context, fixture.Token));
        }
        else Assert.Null(change.BeforeSnapshot);
        Assert.NotNull(change.AfterSnapshot);
        var savedAfter = await reader.ReadAsync(change.AfterSnapshot, context, fixture.Token);
        Assert.Equal(await File.ReadAllBytesAsync(fixture.Path, fixture.Token), savedAfter);
        Assert.Equal(exists ? 2 : 1, Directory.GetFiles(context.ArtifactDirectory!).Length);
        await File.WriteAllTextAsync(fixture.Path, "外部修改", fixture.Token);
        Assert.Equal(savedAfter, await reader.ReadAsync(change.AfterSnapshot, context, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdenticalWrite_DoesNotCreateUnusedHistory(bool replace)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("unchanged");
        var context = Context(fixture);
        var result = replace
            ? await fixture.ExecuteAsync(fixture.Replace, new { path = fixture.Path, oldText = "unchanged", newText = "unchanged" }, context)
            : await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "unchanged" }, context);
        Assert.True(result.IsSuccess);
        Assert.Null(result.FileChanges);
        Assert.False(Directory.Exists(context.ArtifactDirectory));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task StagingEncodingFailure_DoesNotCreateOriginalSnapshot()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture);
        var version = await fixture.SnapshotAsync();
        await Assert.ThrowsAsync<EncoderFallbackException>(() => fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
            "\ud800", new UTF8Encoding(false, true), version, context, fixture.Token));
        Assert.Equal("original", await fixture.ContentsAsync());
        Assert.False(Directory.Exists(context.ArtifactDirectory));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("encoding")]
    [InlineData("commit")]
    public async Task FollowUpWithoutCommit_PreservesEarlierHistory(string ending)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture);
        var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "updated" }, context);
        var change = Assert.Single(result.FileChanges!);
        var version = await fixture.SnapshotAsync();
        if (ending == "unchanged")
        {
            var unchanged = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "updated" }, context);
            Assert.True(unchanged.IsSuccess);
            Assert.Null(unchanged.FileChanges);
        }
        else if (ending == "encoding")
            await Assert.ThrowsAsync<EncoderFallbackException>(() => fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
                "\ud800", new UTF8Encoding(false, true), version, context, fixture.Token));
        else
        {
            using var locked = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
                "failed update", new UTF8Encoding(false), version, context, fixture.Token));
        }
        Assert.Equal("updated", await fixture.ContentsAsync());
        Assert.Equal(2, Directory.GetFiles(context.ArtifactDirectory!).Length);
        var reader = new FileChangeSnapshotReader();
        Assert.Equal(Encoding.UTF8.GetBytes("original"), await reader.ReadAsync(change.BeforeSnapshot!, context, fixture.Token));
        Assert.Equal(Encoding.UTF8.GetBytes("updated"), await reader.ReadAsync(change.AfterSnapshot!, context, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task FailedAtomicCommit_DeletesBothUncommittedSnapshots()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture);
        var version = await fixture.SnapshotAsync();
        using (var locked = new FileStream(fixture.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAnyAsync<IOException>(() => fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
                "updated", Encoding.UTF8, version, context, fixture.Token));
        Assert.Equal("original", await fixture.ContentsAsync());
        Assert.Empty(Directory.GetFiles(context.ArtifactDirectory!));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedWrite_DoesNotCreateHistory(bool outside)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture) with { ApprovalPolicy = outside ? ApprovalPolicy.FullAccess : ApprovalPolicy.ReadOnly };
        var result = await fixture.ExecuteAsync(fixture.Write,
            new { path = outside ? "../outside.txt" : fixture.Path, content = "updated" }, context);
        Assert.False(result.IsSuccess);
        Assert.Null(result.FileChanges);
        Assert.False(Directory.Exists(context.ArtifactDirectory));
        Assert.Equal("original", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task PreCancelledWrite_DoesNotCreateHistoryOrChangeTheFile()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture);
        var version = await fixture.SnapshotAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
            "updated", Encoding.UTF8, version, context, cancellation.Token));
        Assert.False(Directory.Exists(context.ArtifactDirectory));
        Assert.Equal("original", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task OversizedBeforeAndAfter_RemainWritableWithoutUnboundedSnapshots()
    {
        using var fixture = new FileEditingFixture();
        var content = new string('x', checked((int)ToolFileSnapshot.MaximumBytes + 1));
        await fixture.SeedAsync(content);
        var context = Context(fixture);
        var version = await fixture.SnapshotAsync();
        var outcome = await fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
            content + "y", new UTF8Encoding(false), version, context, fixture.Token);
        Assert.NotNull(outcome.Change);
        Assert.Null(outcome.Change.BeforeSnapshot);
        Assert.Null(outcome.Change.AfterSnapshot);
        Assert.False(Directory.Exists(context.ArtifactDirectory));
        Assert.Equal(content + "y", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task ExactLimit_ProducesBoundedSnapshots()
    {
        using var fixture = new FileEditingFixture();
        var content = new string('x', checked((int)ToolFileSnapshot.MaximumBytes));
        await fixture.SeedAsync(content);
        var context = Context(fixture);
        var outcome = await fixture.Writer.WriteTextWithHistoryAsync(fixture.Path,
            content[..^1] + "y", new UTF8Encoding(false), await fixture.SnapshotAsync(), context, fixture.Token);
        Assert.NotNull(outcome.Change);
        Assert.Equal(ToolFileSnapshot.MaximumBytes, outcome.Change.BeforeSnapshot!.Length);
        Assert.Equal(ToolFileSnapshot.MaximumBytes, outcome.Change.AfterSnapshot!.Length);
        Assert.True(outcome.Change.IsValid);
    }

    private static ToolExecutionContext Context(FileEditingFixture fixture) => fixture.Context with
    {
        ArtifactDirectory = Path.Combine(fixture.Root, "artifacts", fixture.Context.TaskId.ToString("N"))
    };
}
