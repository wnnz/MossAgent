using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileEditingSafetyTests
{
    [Fact]
    public async Task PartialReadReturnsFullFileHashAndAutomaticallyProtectsLaterWrite()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha beta gamma");
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("alpha beta gamma")));
        var read = await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path, length = 5 });
        Assert.True(read.IsSuccess, read.Summary);
        Assert.Equal("alpha", read.Content);
        Assert.Contains(expectedHash, read.Summary, StringComparison.Ordinal);
        await fixture.SeedAsync("external change");

        var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "agent change" });

        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Equal("external change", await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task ReplaceRejectsExternalChangeEvenWhenSearchTextStillMatches()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha original tail");
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        await fixture.SeedAsync("alpha externally edited tail");

        var result = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "alpha", newText = "agent change" });

        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Equal("alpha externally edited tail", await fixture.ContentsAsync());
    }

    [Theory]
    [InlineData("write")]
    [InlineData("replace")]
    public async Task ExplicitHashRejectsADeletedPreviouslyReadFile(string operation)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        var snapshot = await fixture.SnapshotAsync();
        File.Delete(fixture.Path);
        var tool = operation == "write" ? (IAgentTool)fixture.Write : fixture.Replace;
        var arguments = new { path = fixture.Path, content = "new", oldText = "original", newText = "new", expectedSha256 = snapshot.Sha256 };

        var result = await fixture.ExecuteAsync(tool, arguments);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(fixture.Path));
        Assert.Equal("file_changed", result.ErrorCode);
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task FreshReadAfterConflictAllowsAReviewedUpdate()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        await fixture.SeedAsync("external change");
        var refused = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "blind retry" });
        Assert.Equal("file_changed", refused.ErrorCode);
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        var reviewed = await fixture.SnapshotAsync();

        var result = await fixture.ExecuteAsync(fixture.Write,
            new { path = fixture.Path, content = "reviewed update", expectedSha256 = reviewed.Sha256!.ToLowerInvariant() });

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal("reviewed update", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task SuccessfulWritesAdvanceTheObservedVersion()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        var first = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "first" });
        var second = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "first", newText = "second" });

        Assert.True(first.IsSuccess, first.Summary);
        Assert.True(second.IsSuccess, second.Summary);
        Assert.Equal("second", await fixture.ContentsAsync());
        var finalVersion = await fixture.SnapshotAsync();
        Assert.Equal(finalVersion, fixture.Context.FileSnapshots.Get(fixture.Context.TaskId, FileSnapshotReader.CacheKey(fixture.Path)));
        Assert.Contains(finalVersion.Sha256!, second.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing", "text_not_found")]
    [InlineData("alpha", "text_not_unique")]
    [InlineData("", "invalid_arguments")]
    public async Task UnsafeSearchDoesNotChangeAnyBytes(string oldText, string errorCode)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha alpha");
        var original = await fixture.SnapshotAsync();

        var result = await fixture.ExecuteAsync(fixture.Replace, new { path = fixture.Path, oldText, newText = "changed" });

        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(original, await fixture.SnapshotAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task DistinctTaskStoresDoNotReplaceEachOthersObservedVersion()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        await fixture.SeedAsync("external change");
        var other = new ToolExecutionContext(Guid.NewGuid(), fixture.Root, [fixture.Root], fixture.Context.ApprovalPolicy);
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path }, other);

        var first = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "first task" });
        var second = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "second task" }, other);

        Assert.Equal("file_changed", first.ErrorCode);
        Assert.True(second.IsSuccess, second.Summary);
        Assert.Equal("second task", await fixture.ContentsAsync());
        Assert.NotSame(fixture.Context.FileSnapshots, other.FileSnapshots);
        Assert.Same(fixture.Context.FileSnapshots, (fixture.Context with { ConversationContext = "child" }).FileSnapshots);
    }

    [Fact]
    public async Task AuthorizedDeleteAllowsSubsequentCreationWithoutStaleCache()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await fixture.ExecuteAsync(fixture.Read, new { path = fixture.Path });
        var deleted = await fixture.ExecuteAsync(new PathDeleteTool(fixture.Paths), new { path = fixture.Path },
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime });
        var created = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content = "new file" });

        Assert.True(deleted.IsSuccess, deleted.Summary);
        Assert.True(created.IsSuccess, created.Summary);
        Assert.Equal("new file", await fixture.ContentsAsync());
    }

    [Fact]
    public async Task ReplacementRejectsOversizedFilesBeforeAllocatingTheText()
    {
        using var fixture = new FileEditingFixture();
        using (var stream = File.Create(fixture.Path)) stream.SetLength(16 * 1024 * 1024 + 1);

        var result = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "original", newText = "new" });

        Assert.Equal("file_too_large", result.ErrorCode);
        Assert.Equal(16 * 1024 * 1024 + 1, new FileInfo(fixture.Path).Length);
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public async Task WritePreservesEmptyAndWhitespaceContent(string content)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("original");

        var result = await fixture.ExecuteAsync(fixture.Write, new { path = fixture.Path, content });

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(content, await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("alpha", "", " beta")]
    [InlineData(" ", "", "alphabeta")]
    [InlineData("alpha", "\t", "\t beta")]
    public async Task ReplacementAllowsExactWhitespaceSearchAndEmptyReplacement(
        string oldText, string newText, string expected)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("alpha beta");

        var result = await fixture.ExecuteAsync(fixture.Replace, new { path = fixture.Path, oldText, newText });

        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(expected, await fixture.ContentsAsync());
        fixture.AssertNoTemporaryFiles();
    }
}
