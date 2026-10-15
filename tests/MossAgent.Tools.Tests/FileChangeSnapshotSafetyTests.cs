using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeSnapshotSafetyTests
{
    [Theory]
    [InlineData("../edit-00000000000000000000000000000000.snapshot", 4)]
    [InlineData("edit-not-a-guid.snapshot", 4)]
    [InlineData("edit-00000000000000000000000000000000.snapshot", -1)]
    [InlineData("edit-00000000000000000000000000000000.snapshot", 2097153)]
    public async Task MalformedReferences_AreRejectedWithoutReading(string name, long length)
    {
        using var fixture = new FileEditingFixture();
        var context = Context(fixture);
        var snapshot = new ToolFileSnapshot(name, length, new string('A', 64));
        var exception = await Assert.ThrowsAsync<FileEditException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, context, fixture.Token));
        Assert.Equal("snapshot_invalid", exception.ErrorCode);
        Assert.False(Directory.Exists(context.ArtifactDirectory));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedSnapshotLengthOrHash_IsRejected(bool length)
    {
        using var fixture = new FileEditingFixture();
        var context = Context(fixture);
        var snapshot = await CreateAsync(fixture, context);
        var path = Path.Combine(context.ArtifactDirectory!, snapshot.FileName);
        await File.WriteAllTextAsync(path, length ? "longer change" : "evil", new UTF8Encoding(false), fixture.Token);
        var exception = await Assert.ThrowsAsync<FileEditException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, context, fixture.Token));
        Assert.Equal("snapshot_invalid", exception.ErrorCode);
    }

    [Fact]
    public async Task OtherTaskDirectoryAndMissingSnapshot_CannotBeUsedAsAFileFallback()
    {
        using var fixture = new FileEditingFixture();
        var context = Context(fixture);
        var snapshot = await CreateAsync(fixture, context);
        var otherTask = Guid.NewGuid();
        var other = new ToolExecutionContext(otherTask, fixture.Root, [fixture.Root], context.ApprovalPolicy,
            ArtifactDirectory: Path.Combine(fixture.Root, "artifacts", otherTask.ToString("N")));
        await Assert.ThrowsAsync<FileEditException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, other, fixture.Token));
        File.Delete(Path.Combine(context.ArtifactDirectory!, snapshot.FileName));
        await Assert.ThrowsAsync<FileEditException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, context, fixture.Token));
        Assert.Equal("body", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task PreCancellation_DoesNotReturnSnapshotBytes()
    {
        using var fixture = new FileEditingFixture();
        var context = Context(fixture);
        var snapshot = await CreateAsync(fixture, context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, context, cancellation.Token));
    }

    [Fact]
    public async Task ArtifactDirectoryJunction_DoesNotReadOrWriteOutsideTheTask()
    {
        using var fixture = new FileEditingFixture();
        using var outside = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = Context(fixture);
        Directory.CreateDirectory(Path.GetDirectoryName(context.ArtifactDirectory!)!);
        await TestDirectoryJunction.CreateAsync(context.ArtifactDirectory!, outside.Root, fixture.Token);
        try
        {
            var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "agent" }, context);
            Assert.False(result.IsSuccess);
            Assert.Equal("original", await fixture.ContentsAsync());
            Assert.Empty(Directory.GetFiles(outside.Root));
            var snapshot = new ToolFileSnapshot("edit-00000000000000000000000000000000.snapshot", 0, new string('A', 64));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new FileChangeSnapshotReader().ReadAsync(snapshot, context, fixture.Token));
        }
        finally { Directory.Delete(context.ArtifactDirectory!); }
    }

    [Fact]
    public async Task SnapshotMetadata_IsBudgetedAsAWholeAndInvalidHashAssociationsAreDropped()
    {
        var hash = new string('A', 64);
        var snapshot = new ToolFileSnapshot("edit-00000000000000000000000000000000.snapshot", 4, hash);
        var change = new ToolFileChange("file.txt", null, hash, AfterSnapshot: snapshot);
        var result = ToolResult.Success("summary") with { FileChanges = [change] };
        var retained = await LimitAsync(result, change.OutputCharacterCount);
        Assert.Equal(change, Assert.Single(retained.FileChanges!));
        Assert.Empty(retained.Summary);
        Assert.True((await LimitAsync(result, change.OutputCharacterCount - 1)).FileChangesTruncated);
        var invalid = change with { AfterSnapshot = snapshot with { Sha256 = new string('B', 64) } };
        Assert.False(invalid.IsValid);
        var limited = await LimitAsync(result with { FileChanges = [invalid] }, 4096);
        Assert.Empty(limited.FileChanges!);
        Assert.True(limited.FileChangesTruncated);
        var serialized = JsonSerializer.Serialize(change);
        Assert.DoesNotContain(nameof(ToolFileSnapshot.IsValid), serialized);
        Assert.DoesNotContain(nameof(ToolFileSnapshot.OutputCharacterCount), serialized);
        Assert.Equal(change, JsonSerializer.Deserialize<ToolFileChange>(serialized));
    }

    private static async Task<ToolFileSnapshot> CreateAsync(FileEditingFixture fixture, ToolExecutionContext context)
    {
        var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "body" }, context);
        return Assert.Single(result.FileChanges!).AfterSnapshot!;
    }

    private static Task<ToolResult> LimitAsync(ToolResult output, int budget)
    {
        var tool = new ToolOutputProbeTool(output);
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
        var context = new ToolExecutionContext(Guid.NewGuid(), Path.GetTempPath(), [Path.GetTempPath()],
            ApprovalPolicy.FullAccess, MaximumOutputCharacters: budget);
        return executor.ExecuteAsync(new ToolRequest("snapshot-output", tool.Descriptor.Name,
            JsonSerializer.SerializeToElement(new { })), context, TestContext.Current.CancellationToken);
    }

    private static ToolExecutionContext Context(FileEditingFixture fixture) => fixture.Context with
    {
        ArtifactDirectory = Path.Combine(fixture.Root, "artifacts", fixture.Context.TaskId.ToString("N"))
    };
}
