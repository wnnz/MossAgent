using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileMutationSnapshotTests
{
    [Theory]
    [InlineData("copy")]
    [InlineData("move")]
    public async Task IntentionalPathMutations_InvalidateAffectedVersions(string operation)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("old destination");
        var source = Path.Combine(fixture.Root, "source.txt");
        await File.WriteAllTextAsync(source, "source content", fixture.Token);
        await fixture.ExecuteAsync(fixture.Read, new { path = source });
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        var sourceVersion = fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(source));
        var mutation = operation == "copy"
            ? await fixture.ExecuteAsync(new PathCopyTool(fixture.Paths),
                new { source, destination = fixture.Path, overwrite = true })
            : await fixture.ExecuteAsync(new PathMoveTool(fixture.Paths),
                new { source, destination = fixture.Path, overwrite = true });

        Assert.True(mutation.IsSuccess, mutation.Summary);
        Assert.Null(fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Path)));
        Assert.Equal(operation == "copy" ? sourceVersion : null,
            fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(source)));
        var result = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "source", newText = "updated" });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("updated content", await fixture.ContentsAsync());
        if (operation == "move")
        {
            var recreate = await fixture.ExecuteAsync(fixture.Write, new { path = source, content = "new source" });
            Assert.True(recreate.IsSuccess, recreate.Summary);
        }
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task WindowsCaseAliases_ShareTheSameObservedVersion()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var read = await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path.ToLowerInvariant() });
        Assert.True(read.IsSuccess, read.Summary);
        await fixture.SeedAsync("external edit");

        var result = await fixture.ExecuteAsync(fixture.Write,
            new { path = fixture.Path.ToUpperInvariant(), content = "agent edit" });

        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Equal("external edit", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }
}
