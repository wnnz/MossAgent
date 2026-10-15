using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeStatisticsTests
{
    [Theory]
    [InlineData("first\nkeep\n", "last\nkeep\n", 1, 1)]
    [InlineData("first\nkeep\n", "first\nkeep\nextra\n", 1, 0)]
    [InlineData("first\nkeep\n", "keep\n", 0, 1)]
    [InlineData("first\r\nkeep\r\n", "last\r\nkeep\r\n", 1, 1)]
    [InlineData("a", "a\n", 1, 1)]
    public async Task Statistics_UseHistoricalVersionsAndDoNotReadTheLiveFile(string before, string after, int added, int removed)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(before: before, after: after);
        await File.WriteAllTextAsync(fixture.Path, "external sentinel", fixture.Token);
        var result = await ExecuteAsync(fixture, change, fixture.Context with { ApprovalPolicy = ApprovalPolicy.ReadOnly });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(new ToolFileChangeStatistics(added, removed), JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!));
        Assert.Null(result.FileChanges);
        Assert.Null(result.Artifacts);
        Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreationAndDeletion_UseTheAbsentVersionRatherThanTheCurrentFile(bool deletion)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(create: true, after: "one\ntwo\n");
        if (deletion) change = new(change.Path, change.AfterSha256, null, change.AfterSnapshot, null, true);
        var result = await ExecuteAsync(fixture, change);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(deletion ? new(0, 2) : new ToolFileChangeStatistics(2, 0),
            JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!));
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Theory]
    [InlineData("utf16")]
    [InlineData("utf32")]
    public async Task EncodedSnapshots_AreCountedAsText(string encoding)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync(encoding == "utf16" ? Encoding.Unicode : Encoding.UTF32);
        var result = await ExecuteAsync(fixture, change);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(new ToolFileChangeStatistics(1, 1), JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!));
    }

    [Theory]
    [InlineData("missing", "snapshot_invalid")]
    [InlineData("hash", "snapshot_invalid")]
    [InlineData("other-task", "snapshot_invalid")]
    [InlineData("legacy", "snapshot_unavailable")]
    [InlineData("outside", "path_denied")]
    [InlineData("traversal", "invalid_arguments")]
    [InlineData("untrusted", "path_denied")]
    [InlineData("denied", "approval_denied")]
    [InlineData("timeout", "tool_timeout")]
    [InlineData("output", "snapshot_review_too_large")]
    public async Task InvalidOrRejectedQueries_DoNotPublishZeroCounts(string scenario, string error)
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var context = fixture.Context;
        if (scenario == "missing") File.Delete(Path.Combine(context.ArtifactDirectory!, change.BeforeSnapshot!.FileName));
        else if (scenario == "hash") await File.WriteAllTextAsync(Path.Combine(context.ArtifactDirectory!, change.BeforeSnapshot!.FileName), "changed", fixture.Token);
        else if (scenario == "other-task") context = context with { TaskId = Guid.NewGuid(), ArtifactDirectory = Path.Combine(fixture.Root, "other") };
        else if (scenario == "legacy") change = change with { BeforeSnapshot = null, AfterSnapshot = null };
        else if (scenario == "outside") change = change with { Path = Path.Combine(fixture.Root, "..", "outside.txt") };
        else if (scenario == "traversal") change = change with { AfterSnapshot = change.AfterSnapshot! with { FileName = "../escape.snapshot" } };
        else if (scenario == "timeout") context = context with { Timeout = TimeSpan.Zero };
        else if (scenario == "output") context = context with { MaximumOutputCharacters = 16 };
        var result = await ExecuteAsync(fixture, change, context, scenario != "denied", scenario != "untrusted");
        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.Content);
        Assert.Null(result.Artifacts);
        Assert.Null(result.FileChanges);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Fact]
    public async Task CancellationAndDescriptor_AreExplicitAndReadOnly()
    {
        using var fixture = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var tool = CreateTool();
        Assert.False(tool.Descriptor.IsModelVisible);
        Assert.Equal(ToolRiskLevel.ReadOnly, tool.Descriptor.RiskLevel);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancelled.Cancel();
        var result = await ExecuteAsync(fixture, change, token: cancelled.Token);
        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.Null(result.Content);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    private static FileChangeStatisticsTool CreateTool() => new(new AuthorizedPathResolver(), new GitCommandRunner(new FixtureTaskProcessLauncher()));

    [Theory]
    [InlineData("FF")]
    [InlineData("0041")]
    [InlineData("FFFE41")]
    public async Task BinaryOrMalformedSnapshots_DoNotProduceStatistics(string hex)
    {
        using var fixture = new FileChangeReviewFixture();
        await File.WriteAllBytesAsync(fixture.Path, Convert.FromHexString(hex), fixture.Token);
        var change = await fixture.ChangeAsync(create: true);
        var result = await ExecuteAsync(fixture, change);
        Assert.Equal("snapshot_review_unsupported", result.ErrorCode);
        Assert.Null(result.Content);
        fixture.AssertNoReviewTemporaryDirectories();
    }

    [Fact]
    public async Task ArtifactJunction_IsDeniedBeforeAccessingAnOutsideDirectory()
    {
        using var fixture = new FileChangeReviewFixture();
        using var outside = new FileChangeReviewFixture();
        var change = await fixture.ChangeAsync();
        var link = Path.Combine(fixture.Root, "linked-artifacts");
        await TestDirectoryJunction.CreateAsync(link, outside.Root, fixture.Token);
        try
        {
            var result = await ExecuteAsync(fixture, change, fixture.Context with { ArtifactDirectory = link });
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Null(result.Content);
            Assert.Empty(Directory.GetFileSystemEntries(outside.Root));
        }
        finally { Directory.Delete(link); }
    }

    private static Task<ToolResult> ExecuteAsync(FileChangeReviewFixture fixture, ToolFileChange change,
        ToolExecutionContext? context = null, bool approve = true, bool trusted = true, CancellationToken? token = null)
    {
        var tool = CreateTool();
        IToolApprovalService approval = approve ? new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)) : new PreviewDenyApprovalService();
        var executor = new ToolExecutor(new ToolRegistry([tool]), approval);
        return executor.ExecuteAsync(FileChangeRestoreFixture.Request(change, tool),
            (context ?? fixture.Context) with { ReviewedChange = trusted ? change : null }, token ?? fixture.Token);
    }
}
