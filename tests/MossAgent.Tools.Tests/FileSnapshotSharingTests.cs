using System.Text;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileSnapshotSharingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenSnapshot_AllowsAtomicEditsAndRetainsItsOriginalBytes(bool replace)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var context = fixture.Context with { ArtifactDirectory = Path.Combine(fixture.Root, "artifacts") };
        await using (var snapshot = FileSnapshotReader.Open(fixture.Path))
        {
            var result = replace
                ? await fixture.ExecuteAsync(fixture.Replace,
                    new { path = fixture.Path, oldText = "original", newText = "updated" }, context)
                : await fixture.ExecuteAsync(fixture.Write,
                    new { path = fixture.Path, content = "updated" }, context);

            Assert.True(result.IsSuccess, result.Summary);
            Assert.Equal("updated", await fixture.ContentsAsync());
            Assert.Equal("original", await ReadAsync(snapshot, fixture.Token));
            var current = await fixture.SnapshotAsync();
            Assert.Equal(current, context.FileSnapshots.Get(context.TaskId, FileSnapshotReader.CacheKey(fixture.Path)));
        }
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task OpenSnapshot_StillRejectsInPlaceWritesOnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await using (var snapshot = FileSnapshotReader.Open(fixture.Path))
        {
            Assert.ThrowsAny<IOException>(() =>
            {
                using var mutation = new FileStream(fixture.Path, FileMode.Open, FileAccess.Write,
                    FileShare.Read | FileShare.Delete);
            });

            Assert.Equal("original", await ReadAsync(snapshot, fixture.Token));
        }
        Assert.Equal("original", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task ExternalAtomicReplacement_StillRejectsTheObservedStaleVersion()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var read = await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        Assert.True(read.IsSuccess, read.Summary);
        await using (var snapshot = FileSnapshotReader.Open(fixture.Path))
        {
            var replacement = Path.Combine(fixture.Root, "replacement.txt");
            await File.WriteAllTextAsync(replacement, "external update", fixture.Token);
            File.Replace(replacement, fixture.Path, destinationBackupFileName: null);

            var result = await fixture.ExecuteAsync(fixture.Write,
                new { path = fixture.Path, content = "stale agent update" });

            Assert.Equal("file_changed", result.ErrorCode);
            Assert.Equal("external update", await fixture.ContentsAsync());
            Assert.Equal("original", await ReadAsync(snapshot, fixture.Token));
        }
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task OpenSnapshot_DoesNotGrantTheTaskDirectoryAccess()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await using (var snapshot = FileSnapshotReader.Open(fixture.Path))
        {
            var result = await fixture.ExecuteAsync(fixture.Write,
                new { path = fixture.Path, content = "unauthorized update" },
                fixture.Context with { AuthorizedRoots = [] });

            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal("original", await ReadAsync(snapshot, fixture.Token));
        }
        Assert.Equal("original", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    private static async Task<string> ReadAsync(Stream stream, CancellationToken token)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), leaveOpen: true);
        return await reader.ReadToEndAsync(token);
    }
}
