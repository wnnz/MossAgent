using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ReadHandleSnapshotTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task WriteChecksAndSnapshotCaptures_RefuseBeforeReadingAndCleanStagedFiles(int deniedCall)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var calls = 0;
        var guard = new ReadHandleProbe { Check = (stream, _) =>
        {
            Assert.Equal(0, stream.Position);
            if (++calls == deniedCall) throw new UnauthorizedAccessException();
        } };
        var directory = Path.Combine(fixture.Root, "artifacts");
        var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "modified" },
            fixture.Context with { ArtifactDirectory = directory, ReadHandleValidator = guard });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(deniedCall, calls);
        Assert.False(guard.Stream!.CanRead);
        Assert.Equal("original", await fixture.ContentsAsync());
        Assert.True(result.FileChanges is null or { Count: 0 });
        if (Directory.Exists(directory)) Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReviewSnapshot_UsesOnlyItsTaskArtifactRootAndChecksBeforeReading(bool deny)
    {
        using var fixture = new FileEditingFixture();
        var directory = Directory.CreateDirectory(Path.Combine(fixture.Root, "artifacts")).FullName;
        var bytes = "snapshot"u8.ToArray();
        var name = $"edit-{Guid.NewGuid():N}.snapshot";
        await File.WriteAllBytesAsync(Path.Combine(directory, name), bytes, fixture.Token);
        var guard = new ReadHandleProbe { Check = (stream, roots) =>
        {
            Assert.Equal(0, stream.Position);
            Assert.Equal(directory, Assert.Single(roots));
            if (deny) throw new UnauthorizedAccessException();
        } };
        var context = fixture.Context with
        {
            AuthorizedRoots = [Path.Combine(fixture.Root, "project")],
            ArtifactDirectory = directory, ReadHandleValidator = guard
        };
        var snapshot = new ToolFileSnapshot(name, bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        var reader = new FileChangeSnapshotReader();
        if (deny) await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync(snapshot, context, fixture.Token));
        else Assert.Equal(bytes, await reader.ReadAsync(snapshot, context, fixture.Token));
        Assert.Equal(1, guard.Calls);
        Assert.False(guard.Stream!.CanRead);
    }
}
